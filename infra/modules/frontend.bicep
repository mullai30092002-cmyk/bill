@description('Static Web App name.')
param staticWebAppName string

@description('Azure region for the Static Web App.')
param location string

@description('Static Web App SKU name.')
param staticWebAppSkuName string = 'Free'

@description('Tags applied to the Static Web App.')
param tags object = {}

resource staticWebApp 'Microsoft.Web/staticSites@2025-03-01' = {
  name: staticWebAppName
  location: location
  kind: 'app'
  tags: tags
  sku: {
    name: staticWebAppSkuName
    tier: staticWebAppSkuName
  }
  properties: {}
}

output staticWebAppName string = staticWebApp.name
output staticWebAppId string = staticWebApp.id
