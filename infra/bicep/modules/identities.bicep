param baseName string
param environmentName string
param githubRepository string
param location string
param tags object

resource deploymentIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2024-11-30' = {
  name: '${baseName}-github-deploy'
  location: location
  tags: tags
}

resource apiIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2024-11-30' = {
  name: '${baseName}-api'
  location: location
  tags: tags
}

resource webIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2024-11-30' = {
  name: '${baseName}-web'
  location: location
  tags: tags
}

resource workerIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2024-11-30' = {
  name: '${baseName}-worker'
  location: location
  tags: tags
}

resource migrationIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2024-11-30' = {
  name: '${baseName}-migration'
  location: location
  tags: tags
}

resource githubEnvironmentCredential 'Microsoft.ManagedIdentity/userAssignedIdentities/federatedIdentityCredentials@2024-11-30' = {
  parent: deploymentIdentity
  name: 'github-${environmentName}'
  properties: {
    audiences: ['api://AzureADTokenExchange']
    issuer: 'https://token.actions.githubusercontent.com'
    subject: 'repo:${githubRepository}:environment:${environmentName}'
  }
}

var contributorRoleId = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'b24988ac-6180-42a0-ab88-20f7382dd24c')
resource deploymentContributor 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(resourceGroup().id, deploymentIdentity.id, contributorRoleId)
  properties: {
    principalId: deploymentIdentity.properties.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: contributorRoleId
  }
}

// Required for this environment-scoped deployment identity to maintain the
// explicit ACR and Key Vault assignments declared by the foundation.
var rbacAdministratorRoleId = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'f58310d9-a9f6-439a-9e8d-f62e7b41a168')
resource deploymentRbacAdministrator 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(resourceGroup().id, deploymentIdentity.id, rbacAdministratorRoleId)
  properties: {
    principalId: deploymentIdentity.properties.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: rbacAdministratorRoleId
  }
}

output deploymentIdentityClientId string = deploymentIdentity.properties.clientId
output registryPrincipalIds array = [webIdentity.properties.principalId, apiIdentity.properties.principalId, workerIdentity.properties.principalId, migrationIdentity.properties.principalId]
output secretPrincipalIds array = [apiIdentity.properties.principalId, workerIdentity.properties.principalId, migrationIdentity.properties.principalId]
output webIdentityId string = webIdentity.id
output apiIdentityId string = apiIdentity.id
output workerIdentityId string = workerIdentity.id
output migrationIdentityId string = migrationIdentity.id
