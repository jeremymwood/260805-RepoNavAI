targetScope = 'subscription'

@allowed(['staging', 'production'])
param environmentName string
param location string
param resourcePrefix string = 'reponav'
param githubRepository string = 'jeremymwood/260805-RepoNavAI'
param alertEmail string
@secure()
param postgresAdministratorPassword string
@secure()
param jwtSigningKey string
param administratorEmail string
@secure()
param administratorPassword string
@secure()
param githubAccessToken string = ''
@secure()
param openAIApiKey string = ''
param applicationUrl string
param postgresSkuName string
param postgresTier string
param postgresStorageSizeGb int
param postgresBackupRetentionDays int
@allowed(['Disabled', 'SameZone', 'ZoneRedundant'])
param postgresHighAvailability string
param monthlyBudgetUsd int
param tags object = {}

var suffix = toLower(environmentName == 'production' ? 'prod' : 'stg')
var resourceGroupName = 'rg-${resourcePrefix}-${suffix}'
var commonTags = union(tags, {
  application: 'RepoNavAI'
  environment: environmentName
  managedBy: 'Bicep'
})

resource environmentResourceGroup 'Microsoft.Resources/resourceGroups@2025-04-01' = {
  name: resourceGroupName
  location: location
  tags: commonTags
}

module foundation 'modules/foundation.bicep' = {
  name: 'foundation-${suffix}'
  scope: environmentResourceGroup
  params: {
    environmentName: environmentName
    location: location
    resourcePrefix: resourcePrefix
    githubRepository: githubRepository
    alertEmail: alertEmail
    postgresAdministratorPassword: postgresAdministratorPassword
    jwtSigningKey: jwtSigningKey
    administratorEmail: administratorEmail
    administratorPassword: administratorPassword
    githubAccessToken: githubAccessToken
    openAIApiKey: openAIApiKey
    applicationUrl: applicationUrl
    postgresSkuName: postgresSkuName
    postgresTier: postgresTier
    postgresStorageSizeGb: postgresStorageSizeGb
    postgresBackupRetentionDays: postgresBackupRetentionDays
    postgresHighAvailability: postgresHighAvailability
    monthlyBudgetUsd: monthlyBudgetUsd
    tags: commonTags
  }
}

output resourceGroupName string = environmentResourceGroup.name
output containerAppsEnvironmentName string = foundation.outputs.containerAppsEnvironmentName
output registryName string = foundation.outputs.registryName
output keyVaultName string = foundation.outputs.keyVaultName
output postgresServerName string = foundation.outputs.postgresServerName
output deploymentIdentityClientId string = foundation.outputs.deploymentIdentityClientId
output webAppName string = foundation.outputs.webAppName
output apiAppName string = foundation.outputs.apiAppName
output workerAppName string = foundation.outputs.workerAppName
output migrationJobName string = foundation.outputs.migrationJobName
output applicationHostname string = foundation.outputs.applicationHostname
