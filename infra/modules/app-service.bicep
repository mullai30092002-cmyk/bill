@description('API App Service name.')
param apiAppName string

@description('App Service plan name.')
param appServicePlanName string

@description('Azure region for the plan and app.')
param location string

@description('App Service SKU name.')
param appServiceSkuName string = 'F1'

@description('App Service SKU tier.')
param appServiceSkuTier string = 'Free'

@description('Tags applied to the plan and app.')
param tags object = {}

resource appServicePlan 'Microsoft.Web/serverfarms@2022-09-01' = {
  name: appServicePlanName
  location: location
  tags: tags
  sku: {
    name: appServiceSkuName
    tier: appServiceSkuTier
    capacity: 1
  }
  properties: {
    hyperV: false
    perSiteScaling: false
    reserved: false
    zoneRedundant: false
  }
}

resource apiApp 'Microsoft.Web/sites@2022-09-01' = {
  name: apiAppName
  location: location
  kind: 'app'
  tags: tags
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    enabled: true
    httpsOnly: true
    publicNetworkAccess: 'Enabled'
    serverFarmId: appServicePlan.id
    siteConfig: {
      ftpsState: 'Disabled'
      http20Enabled: true
      managedPipelineMode: 'Integrated'
      minTlsVersion: '1.2'
      publicNetworkAccess: 'Enabled'
      scmMinTlsVersion: '1.2'
      windowsFxVersion: 'DOTNETCORE|8.0'
      webSocketsEnabled: false
    }
  }
}

output apiAppName string = apiApp.name
output apiAppId string = apiApp.id
output principalId string = apiApp.identity.principalId
output defaultHostName string = apiApp.properties.defaultHostName
output appServicePlanId string = appServicePlan.id
