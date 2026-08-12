param name string
param location string
param runtimePrincipalIds array
param tags object

resource registry 'Microsoft.ContainerRegistry/registries@2025-04-01' = {
  name: name
  location: location
  tags: tags
  sku: { name: 'Standard' }
  properties: {
    adminUserEnabled: false
    anonymousPullEnabled: false
    dataEndpointEnabled: false
    policies: { retentionPolicy: { days: 14, status: 'enabled' } }
  }
}

var acrPullRoleId = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '7f951dda-4ed3-4680-a7ca-43fe172d538d')
resource runtimePullAssignments 'Microsoft.Authorization/roleAssignments@2022-04-01' = [for principalId in runtimePrincipalIds: {
  name: guid(registry.id, principalId, acrPullRoleId)
  scope: registry
  properties: { principalId: principalId, principalType: 'ServicePrincipal', roleDefinitionId: acrPullRoleId }
}]

output name string = registry.name
