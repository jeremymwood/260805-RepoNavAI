param name string
param location string
param infrastructureSubnetId string
param logAnalyticsCustomerId string
@secure()
param logAnalyticsSharedKey string
param tags object

resource environment 'Microsoft.App/managedEnvironments@2025-07-01' = {
  name: name
  location: location
  tags: tags
  properties: {
    appLogsConfiguration: { destination: 'log-analytics', logAnalyticsConfiguration: { customerId: logAnalyticsCustomerId, sharedKey: logAnalyticsSharedKey } }
    vnetConfiguration: { infrastructureSubnetId: infrastructureSubnetId, internal: false }
    workloadProfiles: [{ name: 'Consumption', workloadProfileType: 'Consumption' }]
    zoneRedundant: false
  }
}

output name string = environment.name
