# Azure resources and first manual deployment

This is the first deployment of the central API. It creates the Azure resources, stores the database password on the App Service, and publishes the API by hand. GitHub Actions (issue 13) will automate this later.

The database password never goes in git. It is generated when you deploy and saved as an App Service connection string.

## What Azure creates

Everything lands in the resource group `rg-moisson`, in Canada Central (the closest public region to Montréal).

A Linux App Service plan (Basic B1, .NET 10) runs the API. The App Service is the public HTTPS URL. Azure Database for PostgreSQL 17 (Flexible Server, Burstable B1ms) is the production database. The connection string named `Moisson` holds the host and password and lives only on the App Service.

## What you need

You need access to the Moisson Montréal Azure subscription from issue 6, the [Azure CLI](https://learn.microsoft.com/cli/azure/install-azure-cli), and the .NET 10 SDK. From the repo root, run `dotnet tool restore` so `dotnet-ef` is available. `zip` and `openssl` are already on macOS and Linux.

## Deploy

From the repo root, signed in to the right subscription:

```bash
az login
az account show
./infra/deploy.sh
```

The script creates `rg-moisson` if it is missing, generates a PostgreSQL password, and deploys [infra/main.bicep](../../infra/main.bicep). It then writes `ConnectionStrings:Moisson` on the App Service, which is the same setting the API already reads locally. A temporary firewall rule lets your current IP run migrations. After Entity Framework has updated the Azure database, the script publishes the API, zip-deploys it, and calls `https://<app>/health` until it returns `Healthy`.

`/health` is anonymous and checks the database. A 200 response means the API is reachable and it can open Azure PostgreSQL.

## Confirm by hand

Replace the host with the one the script printed:

```bash
curl -i https://<web-app-host>/health
```

You should get `200` and the body `Healthy`.

In the Azure portal, open the App Service, then Settings, Environment variables, Connection strings. `Moisson` is there. It is not in `appsettings.json`.

## Secrets

The Azure PostgreSQL password lives on the App Service connection string `Moisson`. The local Docker password is only in `appsettings.Development.json` (`moisson` / `moisson`).

Do not paste the Azure password into the repo, a pull request, or Discord. To rotate it, set a new password on the Flexible Server and update the App Service connection string.

Entra ID tenant and client ids are not secrets. They already live in `appsettings.json`.

[infra/main.bicep](../../infra/main.bicep) describes the plan, the app and PostgreSQL. [infra/main.parameters.json](../../infra/main.parameters.json) holds the non-secret defaults (region and names). [infra/deploy.sh](../../infra/deploy.sh) is the first manual deployment.

Issue 13 will reuse the same App Service. It should read the connection string that is already on the app, not a new copy in GitHub.
