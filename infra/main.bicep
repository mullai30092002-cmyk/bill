targetScope = 'resourceGroup'

@description('Deployment environment name.')
@allowed([
  'dev'
  'stage'
  'prod'
])
param environmentName string

@description('Azure region for all resources.')
param location string = resourceGroup().location

@description('Short resource prefix used in Azure resource names.')
param resourcePrefix string = 'billsoft'

@description('Azure SQL database name.')
param sqlDatabaseName string = 'BillSoftDb'

@description('Azure SQL administrator login name.')
param sqlAdministratorLogin string = 'billsoftsqladmin'

@secure()
@description('Azure SQL administrator password. Supply through a secure pipeline variable.')
param sqlAdministratorPassword string

@description('App Service SKU name for the API App Service.')
param appServiceSkuName string = 'F1'

@description('App Service SKU tier for the API App Service.')
param appServiceSkuTier string = 'Free'

@description('Azure SQL database SKU name.')
param sqlDatabaseSkuName string = 'Basic'

@description('Azure SQL database SKU tier.')
param sqlDatabaseTier string = 'Basic'

@description('Azure SQL database capacity.')
param sqlDatabaseCapacity int = 5

@description('Azure Static Web App SKU name.')
param staticWebAppSkuName string = 'Free'

@description('Azure region for the Static Web App resource.')
param staticWebAppLocation string = 'eastus2'

@description('Deploy Key Vault for the environment.')
param enableKeyVault bool = false

@description('Deploy monitoring resources for the environment.')
param enableMonitoring bool = false

@description('Tags applied to all resources.')
param tags object = {}

var nameSeed = take(uniqueString(resourceGroup().id, resourcePrefix, environmentName), 6)
var baseName = toLower(replace('${resourcePrefix}-${environmentName}', '_', '-'))
var apiAppName = take(toLower('${baseName}-api-${nameSeed}'), 60)
var appServicePlanName = take(toLower('${baseName}-plan-${nameSeed}'), 40)
var sqlServerName = take(toLower('${baseName}-sql-${nameSeed}'), 63)
var keyVaultName = take(toLower('${baseName}-kv-${nameSeed}'), 24)
var logAnalyticsName = take(toLower('${baseName}-law-${nameSeed}'), 63)
var appInsightsName = take(toLower('${baseName}-appi-${nameSeed}'), 64)
var staticWebAppName = take(toLower('${baseName}-web-${nameSeed}'), 40)

module monitoring 'modules/monitoring.bicep' = if (enableMonitoring) {
  name: 'monitoring'
  params: {
    appInsightsName: appInsightsName
    location: location
    logAnalyticsName: logAnalyticsName
    tags: tags
  }
}

module appService 'modules/app-service.bicep' = {
  name: 'appService'
  params: {
    apiAppName: apiAppName
    appServicePlanName: appServicePlanName
    appServiceSkuName: appServiceSkuName
    appServiceSkuTier: appServiceSkuTier
    location: location
    tags: tags
  }
}

module sql 'modules/sql.bicep' = {
  name: 'sql'
  params: {
    location: location
    sqlAdministratorLogin: sqlAdministratorLogin
    sqlAdministratorPassword: sqlAdministratorPassword
    sqlDatabaseName: sqlDatabaseName
    sqlServerName: sqlServerName
    sqlDatabaseCapacity: sqlDatabaseCapacity
    sqlDatabaseSkuName: sqlDatabaseSkuName
    sqlDatabaseTier: sqlDatabaseTier
    tags: tags
  }
}

module keyVault 'modules/key-vault.bicep' = if (enableKeyVault) {
  name: 'keyVault'
  params: {
    keyVaultName: keyVaultName
    location: location
    tags: tags
  }
}

module frontend 'modules/frontend.bicep' = {
  name: 'frontend'
  params: {
    location: staticWebAppLocation
    staticWebAppName: staticWebAppName
    staticWebAppSkuName: staticWebAppSkuName
    tags: tags
  }
}

output apiAppName string = appService.outputs.apiAppName
output apiDefaultHostName string = appService.outputs.defaultHostName
output apiPrincipalId string = appService.outputs.principalId
output keyVaultName string = keyVaultName
output sqlDatabaseName string = sql.outputs.sqlDatabaseName
output sqlServerName string = sql.outputs.sqlServerName
output staticWebAppName string = frontend.outputs.staticWebAppName
