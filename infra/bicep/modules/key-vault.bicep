param name string
param location string
param runtimePrincipalIds array
@secure()
param postgresAdministratorPassword string
param tags object

resource vault 'Microsoft.KeyVault/vaults@2024-11-01' = {
  name: name
  location: location
  tags: tags
  properties: {
    tenantId: tenant().tenantId
    enableRbacAuthorization: true
    enablePurgeProtection: true
    enableSoftDelete: true
    softDeleteRetentionInDays: 90
    publicNetworkAccess: 'Enabled'
    sku: { family: 'A', name: 'standard' }
  }
}

resource postgresPassword 'Microsoft.KeyVault/vaults/secrets@2024-11-01' = {
  parent: vault
  name: 'postgres-administrator-password'
  properties: {
    value: postgresAdministratorPassword
    contentType: 'PostgreSQL bootstrap credential; rotate after provisioning'
    attributes: { exp: dateTimeToEpoch('2027-02-01T00:00:00Z') }
  }
}

var secretsUserRoleId = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '4633458b-17de-408a-b874-0445c86b69e6')
resource secretReaders 'Microsoft.Authorization/roleAssignments@2022-04-01' = [for principalId in runtimePrincipalIds: {
  name: guid(vault.id, principalId, secretsUserRoleId)
  scope: vault
  properties: { principalId: principalId, principalType: 'ServicePrincipal', roleDefinitionId: secretsUserRoleId }
}]

output name string = vault.name
