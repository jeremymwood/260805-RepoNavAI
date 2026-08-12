param name string
param location string
param delegatedSubnetId string
param privateDnsZoneId string
@secure()
param administratorPassword string
param skuName string
param tier string
param storageSizeGb int
param backupRetentionDays int
param highAvailability string
param tags object

resource server 'Microsoft.DBforPostgreSQL/flexibleServers@2025-08-01' = {
  name: name
  location: location
  tags: tags
  sku: { name: skuName, tier: tier }
  properties: {
    administratorLogin: 'reponavadministrator'
    administratorLoginPassword: administratorPassword
    version: '17'
    storage: { storageSizeGB: storageSizeGb, autoGrow: 'Enabled' }
    backup: { backupRetentionDays: backupRetentionDays, geoRedundantBackup: 'Disabled' }
    highAvailability: { mode: highAvailability }
    network: { delegatedSubnetResourceId: delegatedSubnetId, privateDnsZoneArmResourceId: privateDnsZoneId, publicNetworkAccess: 'Disabled' }
    authConfig: { activeDirectoryAuth: 'Disabled', passwordAuth: 'Enabled' }
  }
}

resource database 'Microsoft.DBforPostgreSQL/flexibleServers/databases@2025-08-01' = {
  parent: server
  name: 'reponav'
  properties: { charset: 'UTF8', collation: 'en_US.utf8' }
}

resource tls 'Microsoft.DBforPostgreSQL/flexibleServers/configurations@2025-08-01' = {
  parent: server
  name: 'ssl_min_protocol_version'
  properties: { source: 'user-override', value: 'TLSv1.2' }
}

resource extensions 'Microsoft.DBforPostgreSQL/flexibleServers/configurations@2025-08-01' = {
  parent: server
  name: 'azure.extensions'
  properties: { source: 'user-override', value: 'VECTOR' }
}

output id string = server.id
output serverId string = server.id
output name string = server.name
