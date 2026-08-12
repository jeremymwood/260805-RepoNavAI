param baseName string
param location string
param alertEmail string
param tags object

resource logs 'Microsoft.OperationalInsights/workspaces@2025-02-01' = {
  name: '${baseName}-logs'
  location: location
  tags: tags
  properties: { retentionInDays: 30, sku: { name: 'PerGB2018' } }
}

resource insights 'Microsoft.Insights/components@2020-02-02' = {
  name: '${baseName}-appinsights'
  location: location
  kind: 'web'
  tags: tags
  properties: { Application_Type: 'web', WorkspaceResourceId: logs.id }
}

resource actionGroup 'Microsoft.Insights/actionGroups@2023-01-01' = {
  name: '${baseName}-operations'
  location: 'global'
  tags: tags
  properties: {
    groupShortName: take(replace(baseName, '-', ''), 12)
    enabled: true
    emailReceivers: [{ name: 'application-owner', emailAddress: alertEmail, useCommonAlertSchema: true }]
  }
}

output logAnalyticsCustomerId string = logs.properties.customerId
@secure()
output logAnalyticsSharedKey string = logs.listKeys().primarySharedKey
output actionGroupId string = actionGroup.id
