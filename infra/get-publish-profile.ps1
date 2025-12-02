<#
.SYNOPSIS
    Retrieves the publish profile for GitHub Actions deployment.

.DESCRIPTION
    This script retrieves the Azure Functions publish profile XML and provides
    instructions for adding it as a GitHub secret for CI/CD deployment.

.PARAMETER FunctionAppName
    Name of the Azure Function App. Default: func-labgateway-prod

.PARAMETER ResourceGroupName
    Name of the Azure Resource Group. Default: rg-labgateway-prod

.PARAMETER OutputFile
    Optional file path to save the publish profile. If not specified, outputs to console.

.PARAMETER CopyToClipboard
    Copy the publish profile to clipboard for easy pasting into GitHub secrets.

.EXAMPLE
    .\get-publish-profile.ps1 -FunctionAppName func-labgateway-prod -CopyToClipboard

.EXAMPLE
    .\get-publish-profile.ps1 -OutputFile ./publish-profile.xml

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
    [string]$FunctionAppName,

    [Parameter()]
    [string]$ResourceGroupName,

    [Parameter()]
    [string]$OutputFile,

    [Parameter()]
    [switch]$CopyToClipboard
)

# Set strict mode for better error handling
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Default naming based on environment
if (-not $FunctionAppName) {
    $FunctionAppName = "func-labgateway-$Environment"
}
if (-not $ResourceGroupName) {
    $ResourceGroupName = "rg-labgateway-$Environment"
}

#region Validation
Write-Host "`n╔══════════════════════════════════════════════════════════════╗" -ForegroundColor Blue
Write-Host "║        Get Publish Profile for GitHub Actions                ║" -ForegroundColor Blue
Write-Host "╚══════════════════════════════════════════════════════════════╝" -ForegroundColor Blue

Write-Host "`n▶ Validating Azure CLI login..." -ForegroundColor Cyan

try {
    $account = az account show 2>$null | ConvertFrom-Json
    if (-not $account) {
        throw "Not logged in"
    }
    Write-Host "  ✓ Logged in as: $($account.user.name)" -ForegroundColor Green
    Write-Host "  ℹ Subscription: $($account.name)" -ForegroundColor Gray
}
catch {
    Write-Error "Not logged in to Azure. Please run 'az login' first."
}
#endregion

#region Get Publish Profile
Write-Host "`n▶ Retrieving publish profile for: $FunctionAppName" -ForegroundColor Cyan

try {
    $publishProfile = az functionapp deployment list-publishing-profiles `
        --name $FunctionAppName `
        --resource-group $ResourceGroupName `
        --xml `
        --output tsv

    if (-not $publishProfile) {
        throw "Failed to retrieve publish profile. Ensure the Function App exists."
    }

    Write-Host "  ✓ Publish profile retrieved successfully" -ForegroundColor Green
}
catch {
    Write-Error "Failed to retrieve publish profile: $_"
}
#endregion

#region Output
if ($OutputFile) {
    $publishProfile | Out-File -FilePath $OutputFile -Encoding utf8
    Write-Host "`n  ✓ Publish profile saved to: $OutputFile" -ForegroundColor Green
}

if ($CopyToClipboard) {
    $publishProfile | Set-Clipboard
    Write-Host "`n  ✓ Publish profile copied to clipboard!" -ForegroundColor Green
}

if (-not $OutputFile -and -not $CopyToClipboard) {
    Write-Host "`n📄 Publish Profile XML:" -ForegroundColor Yellow
    Write-Host "════════════════════════════════════════════════════════════════" -ForegroundColor Gray
    Write-Host $publishProfile
    Write-Host "════════════════════════════════════════════════════════════════" -ForegroundColor Gray
}
#endregion

#region Instructions
Write-Host "`n╔══════════════════════════════════════════════════════════════╗" -ForegroundColor Green
Write-Host "║              GitHub Secret Setup Instructions                ║" -ForegroundColor Green
Write-Host "╚══════════════════════════════════════════════════════════════╝" -ForegroundColor Green

Write-Host @"

📝 To add the publish profile as a GitHub secret:

   1. Go to your GitHub repository:
      https://github.com/Genomics-Partnership-Wales/LabGateway

   2. Navigate to: Settings → Secrets and variables → Actions

   3. Click "New repository secret"

   4. Configure the secret:
      • Name:  AZURE_FUNCTIONAPP_PUBLISH_PROFILE
      • Value: (paste the entire XML content above)

   5. Click "Add secret"

🔒 Security Notes:
   • The publish profile contains credentials - treat it as sensitive
   • Rotate the publish profile periodically for security
   • Consider using Azure OIDC authentication for production

🔄 To regenerate the publish profile (invalidates old one):
   az functionapp deployment list-publishing-credentials --name $FunctionAppName --resource-group $ResourceGroupName --query scmUri

"@ -ForegroundColor White

# Quick command to copy to clipboard
if (-not $CopyToClipboard) {
    Write-Host "💡 Quick tip: Run with -CopyToClipboard to copy directly:" -ForegroundColor Gray
    Write-Host "   .\get-publish-profile.ps1 -Environment $Environment -CopyToClipboard" -ForegroundColor Gray
}
#endregion
