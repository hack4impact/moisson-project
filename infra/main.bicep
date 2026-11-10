@description('Azure region. Canada Central is the closest public region to Montréal.')
param location string = 'canadacentral'

@description('Short prefix used in resource names.')
param prefix string = 'moisson'

@description('PostgreSQL admin login. The password is never stored in git.')
param postgresAdminLogin string = 'moissonadmin'

@secure()
@description('PostgreSQL admin password. Passed at deploy time, then stored on the App Service.')
param postgresPassword string

var uniqueSuffix = uniqueString(resourceGroup().id)
var planName = '${prefix}-api-plan'
var webAppName = '${prefix}-api-${uniqueSuffix}'
var postgresName = '${prefix}-pg-${uniqueSuffix}'
var databaseName = 'moisson'

var connectionString = 'Host=${postgres.properties.fullyQualifiedDomainName};Database=${databaseName};Username=${postgresAdminLogin};Password=${postgresPassword};SSL Mode=Require;Trust Server Certificate=true'

resource plan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: planName
  location: location
  kind: 'linux'
  sku: {
    name: 'B1'
    tier: 'Basic'
  }
  properties: {
    reserved: true
  }
}

resource webApp 'Microsoft.Web/sites@2023-12-01' = {
  name: webAppName
  location: location
  kind: 'app,linux'
  properties: {
    serverFarmId: plan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|10.0'
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
      appSettings: [
        {
          name: 'ASPNETCORE_ENVIRONMENT'
          value: 'Production'
        }
        {
          name: 'WEBSITE_RUN_FROM_PACKAGE'
          value: '1'
        }
      ]
      connectionStrings: [
        {
          name: 'Moisson'
          connectionString: connectionString
          type: 'PostgreSQL'
        }
      ]
    }
  }
}

resource postgres 'Microsoft.DBforPostgreSQL/flexibleServers@2024-08-01' = {
  name: postgresName
  location: location
  sku: {
    name: 'Standard_B1ms'
    tier: 'Burstable'
  }
  properties: {
    version: '17'
    administratorLogin: postgresAdminLogin
    administratorLoginPassword: postgresPassword
    storage: {
      storageSizeGB: 32
    }
    backup: {
      backupRetentionDays: 7
      geoRedundantBackup: 'Disabled'
    }
    highAvailability: {
      mode: 'Disabled'
    }
    network: {
      publicNetworkAccess: 'Enabled'
    }
    authConfig: {
      activeDirectoryAuth: 'Disabled'
      passwordAuth: 'Enabled'
    }
  }
}

resource postgresDatabase 'Microsoft.DBforPostgreSQL/flexibleServers/databases@2024-08-01' = {
  parent: postgres
  name: databaseName
  properties: {
    charset: 'UTF8'
    collation: 'en_US.utf8'
  }
}

resource allowAzureServices 'Microsoft.DBforPostgreSQL/flexibleServers/firewallRules@2024-08-01' = {
  parent: postgres
  name: 'AllowAzureServices'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

output webAppName string = webApp.name
output webAppHost string = webApp.properties.defaultHostName
output postgresName string = postgres.name
output postgresFqdn string = postgres.properties.fullyQualifiedDomainName
output databaseName string = databaseName
output postgresAdminLogin string = postgresAdminLogin
