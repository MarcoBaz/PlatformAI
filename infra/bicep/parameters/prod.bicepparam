using '../main.bicep'

param environmentName = 'prod'
param acrName         = 'platformaiacr'
param appServiceSku   = 'P1v3'
param imageTag        = 'latest'   // Sovrascritta dalla pipeline con $(Build.BuildId)
