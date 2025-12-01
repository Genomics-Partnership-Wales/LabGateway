<#
.SYNOPSIS
    Deploys Azure resources for LabResultsGateway Azure Functions.

.DESCRIPTION
    This script provisions all required Azure resources for the LabResultsGateway solution:
    - Resource Group
    - Storage Account (for Functions runtime and blob/queue triggers)
    - Application Insights (for monitoring)
    - Function App (.NET 10 Isolated Worker)
    - Required blob containers and queues

.PARAMETER ResourceGroupName
    Name of the Azure Resource Group. Default: rg-labgateway-prod

.PARAMETER Location
    Azure region for resources. Default: uksouth

.PARAMETER Environment
    Environment name (dev, staging, prod). Default: prod

.PARAMETER SkipConfirmation
    Skip confirmation prompts for resource creation.

.EXAMPLE
    .\deploy-azure-resources.ps1 -Environment prod -Location uksouth

.EXAMPLE
    .\deploy-azure-resources.ps1 -Environment dev -SkipConfirmation

.NOTES
    Author: Genomics Partnership Wales
    Requires: Azure CLI (az) installed and logged in
#>

[CmdletBinding()]
param(
    [Parameter()]
    [ValidateSet('dev', 'staging', 'prod')]
    [string]$Environment = 'prod',

    [Parameter()]
    [string]$Location = 'uksouth',

    [Parameter()]
    [string]$ResourceGroupName,

    [Parameter()]
    [switch]$SkipConfirmation
)

# Set strict mode for better error handling
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

#region Configuration
# Generate unique suffix for globally unique resource names
$uniqueSuffix = (Get-FileHash -InputStream ([System.IO.MemoryStream]::new([System.Text.Encoding]::UTF8.GetBytes("labgateway-$Environment"))) -Algorithm MD5).Hash.Substring(0, 6).ToLower()

# Resource naming convention
$config = @{
    ResourceGroup  = if ($ResourceGroupName) { $ResourceGroupName } else { "rg-labgateway-$Environment" }
    StorageAccount = "stlabgw$Environment$uniqueSuffix"
    FunctionApp    = "func-labgateway-$Environment"
    AppInsights    = "appi-labgateway-$Environment"
    AppServicePlan = "asp-labgateway-$Environment"
    Location       = $Location
    Environment    = $Environment

    # Storage containers and queues
    BlobContainers = @('lab-results', 'lab-results-processed', 'lab-results-archive')
    Queues         = @('lab-results-queue', 'lab-results-queue-poison', 'lab-results-retry')

    # Tags for resource management
    Tags           = "environment=$Environment project=labgateway team=genomics-partnership-wales"
}
#endregion

#region Helper Functions
function Write-Step {
    param([string]$Message)
    Write-Host "`n▶ $Message" -ForegroundColor Cyan
}

function Write-Success {
    param([string]$Message)
    Write-Host "  ✓ $Message" -ForegroundColor Green
}

function Write-Info {
    param([string]$Message)
    Write-Host "  ℹ $Message" -ForegroundColor Gray
}

function Test-AzCliInstalled {
    try {
        $null = az version 2>$null
        return $true
    }
    catch {
        return $false
    }
}

function Test-AzLoggedIn {
    try {
        $account = az account show 2>$null | ConvertFrom-Json
        return $null -ne $account
    }
    catch {
        return $false
    }
}
#endregion

#region Validation
Write-Host "`n╔══════════════════════════════════════════════════════════════╗" -ForegroundColor Blue
Write-Host "║     LabResultsGateway Azure Deployment Script                ║" -ForegroundColor Blue
Write-Host "╚══════════════════════════════════════════════════════════════╝" -ForegroundColor Blue

Write-Step "Validating prerequisites..."

if (-not (Test-AzCliInstalled)) {
    Write-Error "Azure CLI is not installed. Please install from https://aka.ms/installazurecli"
}
Write-Success "Azure CLI is installed"

if (-not (Test-AzLoggedIn)) {
    Write-Error "Not logged in to Azure. Please run 'az login' first."
}
$currentAccount = az account show | ConvertFrom-Json
Write-Success "Logged in as: $($currentAccount.user.name)"
Write-Info "Subscription: $($currentAccount.name) ($($currentAccount.id))"
#endregion

#region Display Configuration
Write-Step "Deployment Configuration"
Write-Host ""
Write-Host "  Environment:        $($config.Environment)" -ForegroundColor Yellow
Write-Host "  Location:           $($config.Location)" -ForegroundColor Yellow
Write-Host "  Resource Group:     $($config.ResourceGroup)" -ForegroundColor Yellow
Write-Host "  Storage Account:    $($config.StorageAccount)" -ForegroundColor Yellow
Write-Host "  Function App:       $($config.FunctionApp)" -ForegroundColor Yellow
Write-Host "  App Insights:       $($config.AppInsights)" -ForegroundColor Yellow
Write-Host ""

if (-not $SkipConfirmation) {
    $confirmation = Read-Host "Do you want to proceed with deployment? (y/N)"
    if ($confirmation -ne 'y' -and $confirmation -ne 'Y') {
        Write-Host "Deployment cancelled." -ForegroundColor Yellow
        exit 0
    }
}
#endregion

#region Create Resources
Write-Step "Creating Resource Group: $($config.ResourceGroup)"
az group create `
    --name $config.ResourceGroup `
    --location $config.Location `
    --tags $config.Tags `
    --output none
Write-Success "Resource Group created"

Write-Step "Creating Storage Account: $($config.StorageAccount)"
az storage account create `
    --name $config.StorageAccount `
    --resource-group $config.ResourceGroup `
    --location $config.Location `
    --sku Standard_LRS `
    --kind StorageV2 `
    --min-tls-version TLS1_2 `
    --allow-blob-public-access false `
    --tags $config.Tags `
    --output none
Write-Success "Storage Account created"

# Get storage connection string
$storageConnectionString = az storage account show-connection-string `
    --name $config.StorageAccount `
    --resource-group $config.ResourceGroup `
    --query connectionString `
    --output tsv

Write-Step "Creating blob containers..."
foreach ($container in $config.BlobContainers) {
    az storage container create `
        --name $container `
        --connection-string $storageConnectionString `
        --output none
    Write-Success "Container created: $container"
}

Write-Step "Creating queues..."
foreach ($queue in $config.Queues) {
    az storage queue create `
        --name $queue `
        --connection-string $storageConnectionString `
        --output none
    Write-Success "Queue created: $queue"
}

Write-Step "Creating Application Insights: $($config.AppInsights)"
az monitor app-insights component create `
    --app $config.AppInsights `
    --location $config.Location `
    --resource-group $config.ResourceGroup `
    --kind web `
    --application-type web `
    --tags $config.Tags `
    --output none
Write-Success "Application Insights created"

# Get Application Insights connection string
$appInsightsConnectionString = az monitor app-insights component show `
    --app $config.AppInsights `
    --resource-group $config.ResourceGroup `
    --query connectionString `
    --output tsv

Write-Step "Creating Function App: $($config.FunctionApp)"
# Note: Using --runtime-version 9 as .NET 10 may not be available yet
# Change to 10 when Azure Functions supports .NET 10 GA
az functionapp create `
    --name $config.FunctionApp `
    --resource-group $config.ResourceGroup `
    --storage-account $config.StorageAccount `
    --consumption-plan-location $config.Location `
    --runtime dotnet-isolated `
    --runtime-version 9 `
    --functions-version 4 `
    --os-type Windows `
    --tags $config.Tags `
    --output none
Write-Success "Function App created"

Write-Step "Configuring Function App settings..."
az functionapp config appsettings set `
    --name $config.FunctionApp `
    --resource-group $config.ResourceGroup `
    --settings `
    "APPLICATIONINSIGHTS_CONNECTION_STRING=$appInsightsConnectionString" `
    "AzureWebJobsStorage=$storageConnectionString" `
    "BlobStorageConnection=$storageConnectionString" `
    "QueueStorageConnection=$storageConnectionString" `
    "FUNCTIONS_WORKER_RUNTIME=dotnet-isolated" `
    --output none
Write-Success "Function App settings configured"

Write-Step "Enabling Managed Identity..."
$identity = az functionapp identity assign `
    --name $config.FunctionApp `
    --resource-group $config.ResourceGroup `
    --query principalId `
    --output tsv
Write-Success "Managed Identity enabled: $identity"
#endregion

#region Output Summary
Write-Host "`n╔══════════════════════════════════════════════════════════════╗" -ForegroundColor Green
Write-Host "║                    Deployment Complete!                      ║" -ForegroundColor Green
Write-Host "╚══════════════════════════════════════════════════════════════╝" -ForegroundColor Green

Write-Host "`n📋 Resource Summary:" -ForegroundColor Cyan
Write-Host "   Resource Group:     $($config.ResourceGroup)"
Write-Host "   Storage Account:    $($config.StorageAccount)"
Write-Host "   Function App:       $($config.FunctionApp)"
Write-Host "   App Insights:       $($config.AppInsights)"
Write-Host "   Managed Identity:   $identity"

Write-Host "`n🔗 URLs:" -ForegroundColor Cyan
Write-Host "   Function App:       https://$($config.FunctionApp).azurewebsites.net"
Write-Host "   Health Endpoint:    https://$($config.FunctionApp).azurewebsites.net/api/health"
Write-Host "   Azure Portal:       https://portal.azure.com/#resource/subscriptions/$($currentAccount.id)/resourceGroups/$($config.ResourceGroup)"

Write-Host "`n📝 Next Steps:" -ForegroundColor Yellow
Write-Host "   1. Get publish profile for GitHub Actions:"
Write-Host "      az functionapp deployment list-publishing-profiles --name $($config.FunctionApp) --resource-group $($config.ResourceGroup) --xml"
Write-Host ""
Write-Host "   2. Add the publish profile as GitHub secret: AZURE_FUNCTIONAPP_PUBLISH_PROFILE"
Write-Host ""
Write-Host "   3. Deploy using Azure Functions Core Tools:"
Write-Host "      cd src/API && func azure functionapp publish $($config.FunctionApp)"
Write-Host ""

# Export variables for use in other scripts
$global:LabGatewayConfig = $config
$global:LabGatewayStorageConnectionString = $storageConnectionString
$global:LabGatewayAppInsightsConnectionString = $appInsightsConnectionString

Write-Host "💡 Tip: Configuration exported to `$LabGatewayConfig variable" -ForegroundColor Gray
#endregion
