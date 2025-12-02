<#
.SYNOPSIS
    Deploys Azure Functions to Azure Container Apps environment.

.DESCRIPTION
    This script creates all necessary Azure resources and deploys the
    LabResultsGateway API as an Azure Function App running on Container Apps.

    Prerequisites:
    - Azure CLI installed and logged in
    - Docker Desktop running
    - .NET SDK installed

.PARAMETER ResourceGroupName
    Name of the resource group to create/use

.PARAMETER Location
    Azure region for deployment (default: uksouth)

.EXAMPLE
    .\deploy-functions-container-apps.ps1 -ResourceGroupName "rg-labgateway-prod"
#>

param(
    [Parameter(Mandatory = $false)]
    [string]$ResourceGroupName = "rg-labgateway-prod",

    [Parameter(Mandatory = $false)]
    [string]$Location = "uksouth"
)

$ErrorActionPreference = "Stop"

# Generate unique suffix based on resource group name
$suffix = $ResourceGroupName.GetHashCode().ToString().Substring(0, 6).Replace("-", "")
$storageName = "stlabgw$suffix".ToLower()
$acrName = "acrlabgw$suffix".ToLower()
$envName = "cae-labgateway"
$funcAppName = "func-labgateway-$suffix"
$identityName = "id-labgateway-$suffix"

Write-Host "=====================================" -ForegroundColor Cyan
Write-Host "Azure Functions on Container Apps Deployment" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Configuration:" -ForegroundColor Yellow
Write-Host "  Resource Group: $ResourceGroupName"
Write-Host "  Location: $Location"
Write-Host "  Storage Account: $storageName"
Write-Host "  Container Registry: $acrName"
Write-Host "  Container Apps Env: $envName"
Write-Host "  Function App: $funcAppName"
Write-Host ""

# Step 1: Create Resource Group
Write-Host "Step 1: Creating Resource Group..." -ForegroundColor Green
az group create --name $ResourceGroupName --location $Location --output none
Write-Host "  ✓ Resource group created" -ForegroundColor Green

# Step 2: Create Storage Account (without shared key access for security)
Write-Host "Step 2: Creating Storage Account..." -ForegroundColor Green
az storage account create `
    --name $storageName `
    --resource-group $ResourceGroupName `
    --location $Location `
    --sku Standard_LRS `
    --allow-blob-public-access false `
    --allow-shared-key-access false `
    --output none
Write-Host "  ✓ Storage account created: $storageName" -ForegroundColor Green

# Step 3: Create Container Registry
Write-Host "Step 3: Creating Container Registry..." -ForegroundColor Green
az acr create `
    --name $acrName `
    --resource-group $ResourceGroupName `
    --location $Location `
    --sku Basic `
    --admin-enabled false `
    --output none
Write-Host "  ✓ Container registry created: $acrName" -ForegroundColor Green

# Step 4: Create Container Apps Environment
Write-Host "Step 4: Creating Container Apps Environment..." -ForegroundColor Green
az containerapp env create `
    --name $envName `
    --resource-group $ResourceGroupName `
    --location $Location `
    --enable-workload-profiles `
    --output none
Write-Host "  ✓ Container Apps environment created: $envName" -ForegroundColor Green

# Step 5: Create User-Assigned Managed Identity
Write-Host "Step 5: Creating Managed Identity..." -ForegroundColor Green
$identityJson = az identity create `
    --name $identityName `
    --resource-group $ResourceGroupName `
    --location $Location `
    --output json
$identity = $identityJson | ConvertFrom-Json
$principalId = $identity.principalId
$clientId = $identity.clientId
$identityResourceId = $identity.id
Write-Host "  ✓ Managed identity created: $identityName" -ForegroundColor Green

# Step 6: Assign roles to the managed identity
Write-Host "Step 6: Assigning roles to managed identity..." -ForegroundColor Green

# Get resource IDs
$acrId = az acr show --name $acrName --query id --output tsv
$storageId = az storage account show --name $storageName --resource-group $ResourceGroupName --query id --output tsv

# Assign AcrPull role
az role assignment create `
    --assignee-object-id $principalId `
    --assignee-principal-type ServicePrincipal `
    --role "AcrPull" `
    --scope $acrId `
    --output none

# Assign Storage Blob Data Owner role
az role assignment create `
    --assignee-object-id $principalId `
    --assignee-principal-type ServicePrincipal `
    --role "Storage Blob Data Owner" `
    --scope $storageId `
    --output none

# Assign Storage Queue Data Contributor role
az role assignment create `
    --assignee-object-id $principalId `
    --assignee-principal-type ServicePrincipal `
    --role "Storage Queue Data Contributor" `
    --scope $storageId `
    --output none

# Assign Storage Table Data Contributor role
az role assignment create `
    --assignee-object-id $principalId `
    --assignee-principal-type ServicePrincipal `
    --role "Storage Table Data Contributor" `
    --scope $storageId `
    --output none

Write-Host "  ✓ Roles assigned to managed identity" -ForegroundColor Green

# Step 7: Build and push Docker image
Write-Host "Step 7: Building and pushing Docker image..." -ForegroundColor Green
$loginServer = az acr show --name $acrName --query loginServer --output tsv

# Log in to ACR
az acr login --name $acrName

# Build and push using ACR Tasks (no local Docker needed)
$imageName = "$loginServer/labgateway-api:latest"
Write-Host "  Building image: $imageName"
az acr build `
    --registry $acrName `
    --image "labgateway-api:latest" `
    --file src/API/Dockerfile `
    .
Write-Host "  ✓ Image built and pushed: $imageName" -ForegroundColor Green

# Step 8: Create Function App on Container Apps
Write-Host "Step 8: Creating Function App on Container Apps..." -ForegroundColor Green

# Wait for role assignments to propagate
Write-Host "  Waiting for role assignments to propagate (30 seconds)..."
Start-Sleep -Seconds 30

# Create the function app without specifying image (uses placeholder)
az functionapp create `
    --name $funcAppName `
    --resource-group $ResourceGroupName `
    --storage-account $storageName `
    --environment $envName `
    --workload-profile-name "Consumption" `
    --functions-version 4 `
    --runtime dotnet-isolated `
    --runtime-version 10.0 `
    --assign-identity $identityResourceId `
    --output none

Write-Host "  ✓ Function app created: $funcAppName" -ForegroundColor Green

# Step 9: Configure the function app to use our container image
Write-Host "Step 9: Configuring container image..." -ForegroundColor Green

# Update site config to use the ACR image with managed identity
$patchBody = @{
    siteConfig = @{
        linuxFxVersion = "DOCKER|$imageName"
        acrUseManagedIdentityCreds = $true
        acrUserManagedIdentityID = $identityResourceId
        appSettings = @(
            @{ name = "DOCKER_REGISTRY_SERVER_URL"; value = "https://$loginServer" }
        )
    }
} | ConvertTo-Json -Depth 10 -Compress

az resource patch `
    --resource-group $ResourceGroupName `
    --name $funcAppName `
    --resource-type "Microsoft.Web/sites" `
    --properties $patchBody `
    --output none

Write-Host "  ✓ Container image configured" -ForegroundColor Green

# Step 10: Configure app settings
Write-Host "Step 10: Configuring app settings..." -ForegroundColor Green

# Remove the default AzureWebJobsStorage connection string (we'll use managed identity)
az functionapp config appsettings delete `
    --name $funcAppName `
    --resource-group $ResourceGroupName `
    --setting-names AzureWebJobsStorage `
    --output none 2>$null

# Set managed identity-based storage connection
az functionapp config appsettings set `
    --name $funcAppName `
    --resource-group $ResourceGroupName `
    --settings `
        "AzureWebJobsStorage__accountName=$storageName" `
        "AzureWebJobsStorage__credential=managedidentity" `
        "AzureWebJobsStorage__clientId=$clientId" `
        "FUNCTIONS_WORKER_RUNTIME=dotnet-isolated" `
    --output none

Write-Host "  ✓ App settings configured" -ForegroundColor Green

# Step 11: Create storage containers and queues
Write-Host "Step 11: Creating storage containers and queues..." -ForegroundColor Green

# Need to use account key temporarily to create containers (or assign yourself rights)
# Enable shared key access temporarily
az storage account update --name $storageName --resource-group $ResourceGroupName --allow-shared-key-access true --output none

$connStr = az storage account show-connection-string --name $storageName --resource-group $ResourceGroupName --query connectionString --output tsv

# Create blob containers
az storage container create --name "lab-results" --connection-string $connStr --output none
az storage container create --name "lab-results-processed" --connection-string $connStr --output none
az storage container create --name "lab-results-archive" --connection-string $connStr --output none

# Create queues
az storage queue create --name "lab-results-queue" --connection-string $connStr --output none
az storage queue create --name "lab-results-poison" --connection-string $connStr --output none
az storage queue create --name "lab-results-dead-letter" --connection-string $connStr --output none

# Disable shared key access again
az storage account update --name $storageName --resource-group $ResourceGroupName --allow-shared-key-access false --output none

Write-Host "  ✓ Storage containers and queues created" -ForegroundColor Green

# Get the function app URL
Write-Host ""
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host "Deployment Complete!" -ForegroundColor Green
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host ""

$funcUrl = az functionapp show --name $funcAppName --resource-group $ResourceGroupName --query defaultHostName --output tsv
Write-Host "Function App URL: https://$funcUrl" -ForegroundColor Yellow
Write-Host "Health Check: https://$funcUrl/api/health" -ForegroundColor Yellow
Write-Host ""
Write-Host "Resources created:" -ForegroundColor Cyan
Write-Host "  - Resource Group: $ResourceGroupName"
Write-Host "  - Storage Account: $storageName"
Write-Host "  - Container Registry: $acrName"
Write-Host "  - Container Apps Env: $envName"
Write-Host "  - Function App: $funcAppName"
Write-Host "  - Managed Identity: $identityName"
Write-Host ""
Write-Host "To delete all resources:" -ForegroundColor Yellow
Write-Host "  az group delete --name $ResourceGroupName --yes --no-wait"
