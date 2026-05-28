// =============================================================
// main.bicep — PlatformAI Infrastructure
//
// Resource Group: AuraAI
// App Service:    IndustrialAI  (https://industrialai.azurewebsites.net)
//
// Risorse gestite:
//   - App Service Plan (Linux, container)
//   - App Service "IndustrialAI" (Web App for Containers)
//   - Azure Container Registry
//   - Azure Key Vault
//   - Application Insights + Log Analytics
//   - User-Assigned Managed Identity (ACR pull + KV read)
// =============================================================

targetScope = 'resourceGroup'

// ── Parameters ───────────────────────────────────────────────
@description('Ambiente: prod o staging')
@allowed(['prod', 'staging'])
param environmentName string = 'prod'

@description('Azure region')
param location string = resourceGroup().location

@description('Nome ACR (solo alfanumerico, max 50 car.)')
@minLength(5)
@maxLength(50)
param acrName string = 'platformaiacr'

@description('SKU App Service Plan')
param appServiceSku string = environmentName == 'prod' ? 'P1v3' : 'B2'

@description('Container image tag da deployare')
param imageTag string = 'latest'

// ── Variables ─────────────────────────────────────────────────
var appServiceName  = 'IndustrialAI'
var appServicePlanName = 'IndustrialAI-plan'
var keyVaultName    = 'platformai-${environmentName}-kv'
var identityName    = 'platformai-${environmentName}-identity'
var lawName         = 'platformai-${environmentName}-law'
var appInsightsName = 'platformai-${environmentName}-ai'

var tags = {
  environment: environmentName
  application: 'PlatformAI'
  managedBy: 'bicep'
  repository: 'MarcoBaz/PlatformAI'
}

// ── Log Analytics Workspace ───────────────────────────────────
resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2022-10-01' = {
  name: lawName
  location: location
  tags: tags
  properties: {
    sku: { name: 'PerGB2018' }
    retentionInDays: 30
  }
}

// ── Application Insights ──────────────────────────────────────
resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: appInsightsName
  location: location
  tags: tags
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logAnalytics.id
  }
}

// ── Azure Container Registry ──────────────────────────────────
resource acr 'Microsoft.ContainerRegistry/registries@2023-01-01-preview' = {
  name: acrName
  location: location
  tags: tags
  sku: {
    name: environmentName == 'prod' ? 'Standard' : 'Basic'
  }
  properties: {
    adminUserEnabled: false   // Usa Managed Identity, non password admin
  }
}

// ── Managed Identity ──────────────────────────────────────────
resource managedIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: identityName
  location: location
  tags: tags
}

// ACR Pull → Managed Identity
resource acrPullRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(acr.id, managedIdentity.id, 'AcrPull')
  scope: acr
  properties: {
    roleDefinitionId: subscriptionResourceId(
      'Microsoft.Authorization/roleDefinitions',
      '7f951dda-4ed3-4680-a7ca-43fe172d538d'  // AcrPull
    )
    principalId: managedIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

// ── Key Vault ─────────────────────────────────────────────────
resource keyVault 'Microsoft.KeyVault/vaults@2023-02-01' = {
  name: keyVaultName
  location: location
  tags: tags
  properties: {
    sku: { family: 'A', name: 'standard' }
    tenantId: subscription().tenantId
    enableRbacAuthorization: true      // RBAC, non access policies
    enableSoftDelete: true
    softDeleteRetentionInDays: 7
    enabledForTemplateDeployment: true
  }
}

// Key Vault Secrets User → Managed Identity
resource kvSecretsUserRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(keyVault.id, managedIdentity.id, 'KVSecretsUser')
  scope: keyVault
  properties: {
    roleDefinitionId: subscriptionResourceId(
      'Microsoft.Authorization/roleDefinitions',
      '4633458b-17de-408a-b874-0445c86b69e6'  // Key Vault Secrets User
    )
    principalId: managedIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

// ── App Service Plan ──────────────────────────────────────────
resource appServicePlan 'Microsoft.Web/serverfarms@2022-09-01' = {
  name: appServicePlanName
  location: location
  tags: tags
  kind: 'linux'
  sku: { name: appServiceSku }
  properties: {
    reserved: true   // Obbligatorio per Linux
  }
}

// ── App Service — IndustrialAI ────────────────────────────────
resource appService 'Microsoft.Web/sites@2022-09-01' = {
  name: appServiceName
  location: location
  tags: tags
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${managedIdentity.id}': {}
    }
  }
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'DOCKER|${acr.properties.loginServer}/platformai:${imageTag}'
      acrUseManagedIdentityCreds: true
      acrUserManagedIdentityID: managedIdentity.properties.clientId
      alwaysOn: environmentName == 'prod'
      healthCheckPath: '/health'
      http20Enabled: true
      ftpsState: 'Disabled'      // Sicurezza: disabilita FTP
      minTlsVersion: '1.2'
      appSettings: [
        {
          name: 'ASPNETCORE_ENVIRONMENT'
          value: 'Production'
        }
        {
          name: 'KeyVault__Uri'
          value: keyVault.properties.vaultUri
        }
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: appInsights.properties.ConnectionString
        }
        {
          name: 'DOCKER_REGISTRY_SERVER_URL'
          value: 'https://${acr.properties.loginServer}'
        }
        {
          name: 'WEBSITES_ENABLE_APP_SERVICE_STORAGE'
          value: 'false'
        }
      ]
    }
  }
}

// Deployment slot "staging" per zero-downtime swap
resource stagingSlot 'Microsoft.Web/sites/slots@2022-09-01' = if (environmentName == 'prod') {
  name: 'staging'
  parent: appService
  location: location
  tags: tags
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${managedIdentity.id}': {}
    }
  }
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'DOCKER|${acr.properties.loginServer}/platformai:${imageTag}'
      acrUseManagedIdentityCreds: true
      acrUserManagedIdentityID: managedIdentity.properties.clientId
      healthCheckPath: '/health'
    }
  }
}

// ── Outputs ───────────────────────────────────────────────────
output appServiceUrl string        = 'https://${appService.properties.defaultHostName}'
output acrLoginServer string       = acr.properties.loginServer
output keyVaultUri string          = keyVault.properties.vaultUri
output appInsightsConnString string = appInsights.properties.ConnectionString
output managedIdentityClientId string = managedIdentity.properties.clientId
