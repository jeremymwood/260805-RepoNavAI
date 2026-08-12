param environmentName string
param location string
param resourcePrefix string
param githubRepository string
param alertEmail string
@secure()
param postgresAdministratorPassword string
param postgresSkuName string
param postgresTier string
param postgresStorageSizeGb int
param postgresBackupRetentionDays int
param postgresHighAvailability string
param monthlyBudgetUsd int
param tags object

var suffix = toLower(environmentName == 'production' ? 'prod' : 'stg')
var uniqueSuffix = uniqueString(subscription().subscriptionId, resourceGroup().id)
var baseName = '${resourcePrefix}-${suffix}'

module network 'network.bicep' = {
  name: 'network'
  params: {
    baseName: baseName
    location: location
    tags: tags
  }
}

module identities 'identities.bicep' = {
  name: 'identities'
  params: {
    baseName: baseName
    environmentName: environmentName
    githubRepository: githubRepository
    location: location
    tags: tags
  }
}

module observability 'observability.bicep' = {
  name: 'observability'
  params: {
    baseName: baseName
    location: location
    alertEmail: alertEmail
    tags: tags
  }
}

module registry 'registry.bicep' = {
  name: 'registry'
  params: {
    name: replace('${resourcePrefix}${suffix}${uniqueSuffix}', '-', '')
    location: location
    runtimePrincipalIds: identities.outputs.runtimePrincipalIds
    tags: tags
  }
}

module secrets 'key-vault.bicep' = {
  name: 'key-vault'
  params: {
    name: take(replace('${resourcePrefix}-${suffix}-${uniqueSuffix}', '-', ''), 24)
    location: location
    runtimePrincipalIds: identities.outputs.runtimePrincipalIds
    postgresAdministratorPassword: postgresAdministratorPassword
    tags: tags
  }
}

module database 'postgres.bicep' = {
  name: 'postgres'
  params: {
    name: take('${baseName}-pg-${uniqueSuffix}', 63)
    location: location
    delegatedSubnetId: network.outputs.postgresSubnetId
    privateDnsZoneId: network.outputs.postgresPrivateDnsZoneId
    administratorPassword: postgresAdministratorPassword
    skuName: postgresSkuName
    tier: postgresTier
    storageSizeGb: postgresStorageSizeGb
    backupRetentionDays: postgresBackupRetentionDays
    highAvailability: postgresHighAvailability
    tags: tags
  }
}

module containerApps 'container-apps-environment.bicep' = {
  name: 'container-apps-environment'
  params: {
    name: '${baseName}-cae'
    location: location
    infrastructureSubnetId: network.outputs.containerAppsSubnetId
    logAnalyticsCustomerId: observability.outputs.logAnalyticsCustomerId
    logAnalyticsSharedKey: observability.outputs.logAnalyticsSharedKey
    tags: tags
  }
}

module governance 'governance.bicep' = {
  name: 'governance'
  params: {
    environmentName: environmentName
    monthlyBudgetUsd: monthlyBudgetUsd
    alertEmail: alertEmail
    postgresServerId: database.outputs.serverId
    actionGroupId: observability.outputs.actionGroupId
  }
}

output containerAppsEnvironmentName string = containerApps.outputs.name
output registryName string = registry.outputs.name
output keyVaultName string = secrets.outputs.name
output postgresServerName string = database.outputs.name
output deploymentIdentityClientId string = identities.outputs.deploymentIdentityClientId
