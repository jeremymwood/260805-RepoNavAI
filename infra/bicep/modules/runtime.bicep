param baseName string
param location string
param environmentId string
param registryServer string
param webIdentityId string
param apiIdentityId string
param workerIdentityId string
param migrationIdentityId string
param vaultUri string
param applicationUrl string
param environmentName string
param tags object

var bootstrapWebImage = 'mcr.microsoft.com/k8se/quickstart:latest'
var bootstrapServiceImage = 'mcr.microsoft.com/k8se/quickstart:latest'
var bootstrapJobImage = 'mcr.microsoft.com/k8se/quickstart-jobs:latest'
var connectionSecret = { name: 'connection-string', keyVaultUrl: '${vaultUri}/secrets/connection-string', identity: apiIdentityId }
var jwtSecret = { name: 'jwt-signing-key', keyVaultUrl: '${vaultUri}/secrets/jwt-signing-key', identity: apiIdentityId }
var githubSecret = { name: 'github-access-token', keyVaultUrl: '${vaultUri}/secrets/github-access-token', identity: apiIdentityId }
var openAISecret = { name: 'openai-api-key', keyVaultUrl: '${vaultUri}/secrets/openai-api-key', identity: apiIdentityId }
var commonApiEnvironment = [
  { name: 'ASPNETCORE_ENVIRONMENT', value: 'Production' }
  { name: 'ASPNETCORE_URLS', value: 'http://+:8080' }
  { name: 'ConnectionStrings__DefaultConnection', secretRef: 'connection-string' }
  { name: 'Jwt__Issuer', value: 'RepoNavAI' }
  { name: 'Jwt__Audience', value: 'RepoNavAI.Web' }
  { name: 'Jwt__SigningKey', secretRef: 'jwt-signing-key' }
  { name: 'GitHub__AccessToken', secretRef: 'github-access-token' }
  { name: 'OpenAI__ApiKey', secretRef: 'openai-api-key' }
  { name: 'OpenAI__EmbeddingModel', value: 'text-embedding-3-small' }
  { name: 'OpenAI__EmbeddingDimensions', value: '512' }
]

resource api 'Microsoft.App/containerApps@2025-01-01' = {
  name: '${baseName}-api'
  location: location
  tags: tags
  identity: { type: 'UserAssigned', userAssignedIdentities: { '${apiIdentityId}': {} } }
  properties: {
    managedEnvironmentId: environmentId
    configuration: {
      activeRevisionsMode: 'Multiple'
      ingress: { external: false, targetPort: 8080, transport: 'auto', traffic: [{ latestRevision: true, weight: 100 }] }
      registries: [{ server: registryServer, identity: apiIdentityId }]
      secrets: [connectionSecret, jwtSecret, githubSecret, openAISecret]
    }
    template: {
      revisionSuffix: 'bootstrap'
      containers: [{
        name: 'api'
        image: bootstrapServiceImage
        env: concat(commonApiEnvironment, [{ name: 'Cors__AllowedOrigins__0', value: applicationUrl }])
        resources: { cpu: json('0.5'), memory: '1Gi' }
        probes: [
          { type: 'Liveness', httpGet: { path: '/health', port: 8080, scheme: 'HTTP' }, initialDelaySeconds: 20, periodSeconds: 20 }
          { type: 'Readiness', httpGet: { path: '/health', port: 8080, scheme: 'HTTP' }, initialDelaySeconds: 5, periodSeconds: 10 }
        ]
      }]
      scale: { minReplicas: environmentName == 'production' ? 1 : 0, maxReplicas: environmentName == 'production' ? 3 : 1 }
    }
  }
}

resource web 'Microsoft.App/containerApps@2025-01-01' = {
  name: '${baseName}-web'
  location: location
  tags: tags
  identity: { type: 'UserAssigned', userAssignedIdentities: { '${webIdentityId}': {} } }
  properties: {
    managedEnvironmentId: environmentId
    configuration: {
      activeRevisionsMode: 'Multiple'
      ingress: { external: true, targetPort: 8080, transport: 'auto', allowInsecure: false, traffic: [{ latestRevision: true, weight: 100 }] }
      registries: [{ server: registryServer, identity: webIdentityId }]
    }
    template: {
      revisionSuffix: 'bootstrap'
      containers: [{
        name: 'web'
        image: bootstrapWebImage
        env: [{ name: 'API_UPSTREAM', value: '${api.name}:8080' }]
        resources: { cpu: json('0.25'), memory: '0.5Gi' }
        probes: [
          { type: 'Liveness', httpGet: { path: '/', port: 8080, scheme: 'HTTP' }, initialDelaySeconds: 10, periodSeconds: 20 }
          { type: 'Readiness', httpGet: { path: '/', port: 8080, scheme: 'HTTP' }, initialDelaySeconds: 5, periodSeconds: 10 }
        ]
      }]
      scale: { minReplicas: environmentName == 'production' ? 1 : 0, maxReplicas: environmentName == 'production' ? 3 : 1 }
    }
  }
}

resource worker 'Microsoft.App/containerApps@2025-01-01' = {
  name: '${baseName}-worker'
  location: location
  tags: tags
  identity: { type: 'UserAssigned', userAssignedIdentities: { '${workerIdentityId}': {} } }
  properties: {
    managedEnvironmentId: environmentId
    configuration: {
      activeRevisionsMode: 'Single'
      registries: [{ server: registryServer, identity: workerIdentityId }]
      secrets: [
        { name: 'connection-string', keyVaultUrl: '${vaultUri}/secrets/connection-string', identity: workerIdentityId }
        { name: 'jwt-signing-key', keyVaultUrl: '${vaultUri}/secrets/jwt-signing-key', identity: workerIdentityId }
        { name: 'github-access-token', keyVaultUrl: '${vaultUri}/secrets/github-access-token', identity: workerIdentityId }
        { name: 'openai-api-key', keyVaultUrl: '${vaultUri}/secrets/openai-api-key', identity: workerIdentityId }
      ]
    }
    template: {
      revisionSuffix: 'bootstrap'
      containers: [{
        name: 'worker'
        image: bootstrapServiceImage
        env: commonApiEnvironment
        resources: { cpu: json('0.5'), memory: '1Gi' }
        probes: [
          { type: 'Liveness', httpGet: { path: '/health/live', port: 8080, scheme: 'HTTP' }, initialDelaySeconds: 20, periodSeconds: 20 }
          { type: 'Readiness', httpGet: { path: '/health/ready', port: 8080, scheme: 'HTTP' }, initialDelaySeconds: 5, periodSeconds: 10 }
        ]
      }]
      scale: { minReplicas: 1, maxReplicas: 1 }
    }
  }
}

resource migration 'Microsoft.App/jobs@2025-01-01' = {
  name: '${baseName}-migration'
  location: location
  tags: tags
  identity: { type: 'UserAssigned', userAssignedIdentities: { '${migrationIdentityId}': {} } }
  properties: {
    environmentId: environmentId
    configuration: {
      triggerType: 'Manual'
      replicaTimeout: 1800
      replicaRetryLimit: 0
      manualTriggerConfig: { parallelism: 1, replicaCompletionCount: 1 }
      registries: [{ server: registryServer, identity: migrationIdentityId }]
      secrets: [
        { name: 'connection-string', keyVaultUrl: '${vaultUri}/secrets/connection-string', identity: migrationIdentityId }
        { name: 'administrator-email', keyVaultUrl: '${vaultUri}/secrets/administrator-email', identity: migrationIdentityId }
        { name: 'administrator-password', keyVaultUrl: '${vaultUri}/secrets/administrator-password', identity: migrationIdentityId }
      ]
    }
    template: {
      containers: [{
        name: 'migration'
        image: bootstrapJobImage
        env: [
          { name: 'ConnectionStrings__DefaultConnection', secretRef: 'connection-string' }
          { name: 'Admin__Email', secretRef: 'administrator-email' }
          { name: 'Admin__Password', secretRef: 'administrator-password' }
        ]
        resources: { cpu: json('0.5'), memory: '1Gi' }
      }]
    }
  }
}

output webAppName string = web.name
output apiAppName string = api.name
output workerAppName string = worker.name
output migrationJobName string = migration.name
output applicationHostname string = web.properties.configuration.ingress.fqdn
