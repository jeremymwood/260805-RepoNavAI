param name string
param location string
param runtimePrincipalIds array
@secure()
param postgresAdministratorPassword string
@secure()
param connectionString string
@secure()
param jwtSigningKey string
param administratorEmail string
@secure()
param administratorPassword string
@secure()
param githubAccessToken string
@secure()
param openAIApiKey string
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

resource applicationSecrets 'Microsoft.KeyVault/vaults/secrets@2024-11-01' = [for secret in [
  { name: 'connection-string', value: connectionString, contentType: 'Npgsql connection string' }
  { name: 'jwt-signing-key', value: jwtSigningKey, contentType: 'JWT HMAC signing material' }
  { name: 'administrator-email', value: administratorEmail, contentType: 'Bootstrap administrator email' }
  { name: 'administrator-password', value: administratorPassword, contentType: 'Bootstrap administrator credential' }
  { name: 'github-access-token', value: githubAccessToken, contentType: 'GitHub repository provider token' }
  { name: 'openai-api-key', value: openAIApiKey, contentType: 'OpenAI provider credential' }
]: {
  parent: vault
  name: secret.name
  properties: {
    value: secret.value
    contentType: secret.contentType
    attributes: { exp: dateTimeToEpoch('2027-02-01T00:00:00Z') }
  }
}]

var secretsUserRoleId = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '4633458b-17de-408a-b874-0445c86b69e6')
resource secretReaders 'Microsoft.Authorization/roleAssignments@2022-04-01' = [for principalId in runtimePrincipalIds: {
  name: guid(vault.id, principalId, secretsUserRoleId)
  scope: vault
  properties: { principalId: principalId, principalType: 'ServicePrincipal', roleDefinitionId: secretsUserRoleId }
}]

output name string = vault.name
