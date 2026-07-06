@description('Azure SQL server name.')
param sqlServerName string

@description('Azure SQL database name.')
param sqlDatabaseName string

@description('Azure region for the SQL resources.')
param location string

@description('Azure SQL administrator login name.')
param sqlAdministratorLogin string

@secure()
@description('Azure SQL administrator password.')
param sqlAdministratorPassword string

@description('Azure SQL database SKU name.')
param sqlDatabaseSkuName string = 'Basic'

@description('Azure SQL database SKU tier.')
param sqlDatabaseTier string = 'Basic'

@description('Azure SQL database capacity.')
param sqlDatabaseCapacity int = 5

@description('Tags applied to SQL resources.')
param tags object = {}

resource sqlServer 'Microsoft.Sql/servers@2023-08-01' = {
  name: sqlServerName
  location: location
  tags: tags
  properties: {
    administratorLogin: sqlAdministratorLogin
    administratorLoginPassword: sqlAdministratorPassword
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
    restrictOutboundNetworkAccess: 'Disabled'
    version: '12.0'
  }
}

resource allowAzureServices 'Microsoft.Sql/servers/firewallRules@2024-11-01-preview' = {
  name: 'AllowAzureServices'
  parent: sqlServer
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

resource sqlDatabase 'Microsoft.Sql/servers/databases@2023-08-01-preview' = {
  parent: sqlServer
  name: sqlDatabaseName
  location: location
  tags: tags
  sku: {
    name: sqlDatabaseSkuName
    tier: sqlDatabaseTier
    capacity: sqlDatabaseCapacity
  }
  properties: {
    catalogCollation: 'SQL_Latin1_General_CP1_CI_AS'
    collation: 'SQL_Latin1_General_CP1_CI_AS'
    zoneRedundant: false
  }
}

output sqlServerName string = sqlServer.name
output sqlDatabaseName string = sqlDatabase.name
output sqlDatabaseId string = sqlDatabase.id
