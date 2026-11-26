#!/usr/bin/env bash
# First manual deployment of the API and PostgreSQL database to Azure.
# Secrets are generated here and stored on the App Service. They are never written to git.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
RESOURCE_GROUP="${RESOURCE_GROUP:-rg-moisson}"
LOCATION="${LOCATION:-canadacentral}"
POSTGRES_ADMIN_LOGIN="${POSTGRES_ADMIN_LOGIN:-moissonadmin}"

if ! command -v az >/dev/null; then
  echo "Install the Azure CLI: https://learn.microsoft.com/cli/azure/install-azure-cli" >&2
  exit 1
fi

if ! az account show >/dev/null 2>&1; then
  echo "Sign in to the Moisson Montréal subscription."
  az login
fi

echo "Subscription: $(az account show --query name -o tsv)"

az group create --name "$RESOURCE_GROUP" --location "$LOCATION" --output none

if [[ -z "${POSTGRES_PASSWORD:-}" ]]; then
  POSTGRES_PASSWORD="$(openssl rand -base64 24 | tr -d '/+=' | cut -c1-24)Aa1"
fi

echo "Creating the App Service and PostgreSQL server (this can take several minutes)..."
az deployment group create \
  --name moisson-manual \
  --resource-group "$RESOURCE_GROUP" \
  --template-file "$ROOT/infra/main.bicep" \
  --parameters "$ROOT/infra/main.parameters.json" \
  --parameters postgresAdminLogin="$POSTGRES_ADMIN_LOGIN" postgresPassword="$POSTGRES_PASSWORD" \
  --output none

WEB_APP_NAME="$(az deployment group show --resource-group "$RESOURCE_GROUP" --name moisson-manual --query properties.outputs.webAppName.value -o tsv)"
WEB_APP_HOST="$(az deployment group show --resource-group "$RESOURCE_GROUP" --name moisson-manual --query properties.outputs.webAppHost.value -o tsv)"
POSTGRES_NAME="$(az deployment group show --resource-group "$RESOURCE_GROUP" --name moisson-manual --query properties.outputs.postgresName.value -o tsv)"
POSTGRES_FQDN="$(az deployment group show --resource-group "$RESOURCE_GROUP" --name moisson-manual --query properties.outputs.postgresFqdn.value -o tsv)"
DATABASE_NAME="$(az deployment group show --resource-group "$RESOURCE_GROUP" --name moisson-manual --query properties.outputs.databaseName.value -o tsv)"

CONNECTION_STRING="Host=${POSTGRES_FQDN};Database=${DATABASE_NAME};Username=${POSTGRES_ADMIN_LOGIN};Password=${POSTGRES_PASSWORD};SSL Mode=Require;Trust Server Certificate=true"

CLIENT_IP="$(curl -fsS https://api.ipify.org)"
az postgres flexible-server firewall-rule create \
  --resource-group "$RESOURCE_GROUP" \
  --name "$POSTGRES_NAME" \
  --rule-name AllowDeployClient \
  --start-ip-address "$CLIENT_IP" \
  --end-ip-address "$CLIENT_IP" \
  --output none

echo "Applying database migrations..."
dotnet ef database update \
  --project "$ROOT/src/MoissonMontreal.Api" \
  --connection "$CONNECTION_STRING"

echo "Publishing the API..."
PUBLISH_DIR="$(mktemp -d)"
trap 'rm -rf "$PUBLISH_DIR"' EXIT
dotnet publish "$ROOT/src/MoissonMontreal.Api/MoissonMontreal.Api.csproj" -c Release -o "$PUBLISH_DIR" --nologo
(
  cd "$PUBLISH_DIR"
  zip -qr "$PUBLISH_DIR/api.zip" .
)

echo "Deploying to App Service $WEB_APP_NAME..."
az webapp deploy \
  --resource-group "$RESOURCE_GROUP" \
  --name "$WEB_APP_NAME" \
  --src-path "$PUBLISH_DIR/api.zip" \
  --type zip \
  --async false \
  --output none

HEALTH_URL="https://${WEB_APP_HOST}/health"
echo "Checking $HEALTH_URL ..."
for _ in 1 2 3 4 5 6 7 8 9 10; do
  if curl -fsS "$HEALTH_URL"; then
    echo
    echo "API is reachable and connected to Azure PostgreSQL."
    echo "App Service: https://${WEB_APP_HOST}"
    echo "The connection string is stored on the App Service (Connection strings: Moisson), not in git."
    exit 0
  fi
  sleep 6
done

echo "The API did not return Healthy. Check App Service logs:" >&2
echo "  az webapp log tail --resource-group $RESOURCE_GROUP --name $WEB_APP_NAME" >&2
exit 1
