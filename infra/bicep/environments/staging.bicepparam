using '../main.bicep'

param environmentName = 'staging'
param location = 'centralus'
param alertEmail = 'replace-with-operator@example.com'
param postgresAdministratorPassword = readEnvironmentVariable('POSTGRES_ADMINISTRATOR_PASSWORD')
param jwtSigningKey = readEnvironmentVariable('JWT_SIGNING_KEY')
param administratorEmail = readEnvironmentVariable('ADMIN_EMAIL')
param administratorPassword = readEnvironmentVariable('ADMIN_PASSWORD')
param githubAccessToken = readEnvironmentVariable('GITHUB_ACCESS_TOKEN')
param openAIApiKey = readEnvironmentVariable('OPENAI_API_KEY')
param applicationUrl = 'https://replace-with-staging-hostname'
param postgresSkuName = 'Standard_B1ms'
param postgresTier = 'Burstable'
param postgresStorageSizeGb = 32
param postgresBackupRetentionDays = 7
param postgresHighAvailability = 'Disabled'
param monthlyBudgetUsd = 100
param tags = { costCenter: 'RepoNavAI', dataClassification: 'internal' }
