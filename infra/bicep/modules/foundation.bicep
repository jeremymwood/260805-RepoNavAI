param environmentName string
param location string
param resourcePrefix string
param githubRepository string
param alertEmail string
@secure()
param postgresAdministratorPassword string
@secure()
param jwtSigningKey string
param administratorEmail string
@secure()
param administratorPassword string
@secure()
param githubAccessToken string
@secure()
param openAIApiKey string
param applicationUrl string
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
var postgresName = take('${baseName}-pg-${uniqueSuffix}', 63)
var registryName = replace('${resourcePrefix}${suffix}${uniqueSuffix}', '-', '')
var vaultName = take(replace('${resourcePrefix}-${suffix}-${uniqueSuffix}', '-', ''), 24)
var connectionString = 'Host=${postgresName}.postgres.database.azure.com;Port=5432;Database=reponav;Username=reponavadministrator;Password=${postgresAdministratorPassword};SSL Mode=Require;Trust Server Certificate=false'

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
    name: registryName
    location: location
    runtimePrincipalIds: identities.outputs.registryPrincipalIds
    tags: tags
  }
}

module secrets 'key-vault.bicep' = {
  name: 'key-vault'
  params: {
    name: vaultName
    location: location
    runtimePrincipalIds: identities.outputs.secretPrincipalIds
    postgresAdministratorPassword: postgresAdministratorPassword
    connectionString: connectionString
    jwtSigningKey: jwtSigningKey
    administratorEmail: administratorEmail
    administratorPassword: administratorPassword
    githubAccessToken: githubAccessToken
    openAIApiKey: openAIApiKey
    tags: tags
  }
}

module database 'postgres.bicep' = {
  name: 'postgres'
  params: {
    name: postgresName
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

module runtime 'runtime.bicep' = {
  name: 'runtime'
  dependsOn: [registry, secrets, database]
  params: {
    baseName: baseName
    location: location
    environmentId: containerApps.outputs.id
    registryServer: '${registryName}.azurecr.io'
    webIdentityId: identities.outputs.webIdentityId
    apiIdentityId: identities.outputs.apiIdentityId
    workerIdentityId: identities.outputs.workerIdentityId
    migrationIdentityId: identities.outputs.migrationIdentityId
    vaultUri: 'https://${vaultName}.vault.azure.net'
    applicationUrl: applicationUrl
    environmentName: environmentName
    tags: tags
  }
}

output containerAppsEnvironmentName string = containerApps.outputs.name
output registryName string = registry.outputs.name
output keyVaultName string = secrets.outputs.name
output postgresServerName string = database.outputs.name
output deploymentIdentityClientId string = identities.outputs.deploymentIdentityClientId
output webAppName string = runtime.outputs.webAppName
output apiAppName string = runtime.outputs.apiAppName
output workerAppName string = runtime.outputs.workerAppName
output migrationJobName string = runtime.outputs.migrationJobName
output applicationHostname string = runtime.outputs.applicationHostname
