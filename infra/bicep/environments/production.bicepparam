using '../main.bicep'

param environmentName = 'production'
param location = 'centralus'
param alertEmail = 'replace-with-operator@example.com'
param postgresAdministratorPassword = readEnvironmentVariable('POSTGRES_ADMINISTRATOR_PASSWORD')
param postgresSkuName = 'Standard_D2ds_v5'
param postgresTier = 'GeneralPurpose'
param postgresStorageSizeGb = 128
param postgresBackupRetentionDays = 35
param postgresHighAvailability = 'Disabled'
param monthlyBudgetUsd = 250
param tags = { costCenter: 'RepoNavAI', dataClassification: 'confidential' }
