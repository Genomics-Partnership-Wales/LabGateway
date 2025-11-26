# Azure Key Vault Setup for Lab Results Gateway

This document provides instructions for setting up Azure Key Vault for production deployment of the Lab Results Gateway Azure Functions application.

## Prerequisites

- Azure subscription with appropriate permissions
- Azure CLI installed and authenticated
- Azure Functions app created in Azure

## Key Vault Creation

### 1. Create Azure Key Vault

```bash
# Set variables
RESOURCE_GROUP="your-resource-group"
LOCATION="uksouth"
KEY_VAULT_NAME="labresults-kv-$(date +%s)"  # Must be globally unique

# Create Key Vault
az keyvault create \
  --name $KEY_VAULT_NAME \
  --resource-group $RESOURCE_GROUP \
  --location $LOCATION \
  --enabled-for-deployment true \
  --enabled-for-template-deployment true
```

### 2. Add Secrets to Key Vault

```bash
# Add API keys as secrets
az keyvault secret set \
  --vault-name $KEY_VAULT_NAME \
  --name "MetadataApiKey" \
  --value "your-metadata-api-key-here"

az keyvault secret set \
  --vault-name $KEY_VAULT_NAME \
  --name "ExternalEndpointApiKey" \
  --value "your-external-endpoint-api-key-here"
```

## Azure Functions App Configuration

### 1. Enable Managed Identity

```bash
# Get Functions app name
FUNCTIONS_APP_NAME="your-functions-app-name"

# Enable system-assigned managed identity
az functionapp identity assign \
  --name $FUNCTIONS_APP_NAME \
  --resource-group $RESOURCE_GROUP
```

### 2. Grant Key Vault Access

```bash
# Get the managed identity principal ID
PRINCIPAL_ID=$(az functionapp identity show \
  --name $FUNCTIONS_APP_NAME \
  --resource-group $RESOURCE_GROUP \
  --query principalId \
  --output tsv)

# Grant Key Vault Secrets User role to the managed identity
az keyvault set-policy \
  --name $KEY_VAULT_NAME \
  --object-id $PRINCIPAL_ID \
  --secret-permissions get list
```

### 3. Configure Application Settings

Update your Azure Functions app settings to use Key Vault references:

```bash
# Set Key Vault URI
az functionapp config appsettings set \
  --name $FUNCTIONS_APP_NAME \
  --resource-group $RESOURCE_GROUP \
  --setting KeyVaultUri="https://$KEY_VAULT_NAME.vault.azure.net/"

# Set Key Vault references for secrets
az functionapp config appsettings set \
  --name $FUNCTIONS_APP_NAME \
  --resource-group $RESOURCE_GROUP \
  --setting MetadataApiKey="@Microsoft.KeyVault(SecretUri=https://$KEY_VAULT_NAME.vault.azure.net/secrets/MetadataApiKey)"

az functionapp config appsettings set \
  --name $FUNCTIONS_APP_NAME \
  --resource-group $RESOURCE_GROUP \
  --setting ExternalEndpointApiKey="@Microsoft.KeyVault(SecretUri=https://$KEY_VAULT_NAME.vault.azure.net/secrets/ExternalEndpointApiKey)"
```

## Local Development Setup

For local development, use plain text values in `local.settings.json`:

```json
{
  "IsEncrypted": false,
  "Values": {
    "KeyVaultUri": "",
    "MetadataApiKey": "[LOCAL-DEV-API-KEY]",
    "ExternalEndpointApiKey": "[LOCAL-DEV-API-KEY]"
  }
}
```

## Security Best Practices

### 1. Access Control
- Use Azure RBAC for Key Vault access management
- Implement least privilege access
- Regularly rotate secrets
- Monitor access logs

### 2. Network Security
- Enable Key Vault firewall if needed
- Use private endpoints for enhanced security
- Restrict access to specific virtual networks

### 3. Monitoring and Auditing
- Enable Key Vault diagnostic logs
- Monitor secret access patterns
- Set up alerts for unusual activity
- Regular security reviews

## Troubleshooting

### Common Issues

1. **"Access denied" errors**
   - Verify managed identity is enabled
   - Check Key Vault access policies
   - Ensure correct Key Vault URI format

2. **Secret not found**
   - Verify secret names match exactly
   - Check Key Vault region and subscription
   - Confirm secret exists and is not expired

3. **Local development issues**
   - Ensure `KeyVaultUri` is empty for local dev
   - Use plain text values in `local.settings.json`
   - Verify Azure CLI authentication for local testing

### Testing Key Vault Integration

```bash
# Test secret access
az keyvault secret show \
  --vault-name $KEY_VAULT_NAME \
  --name "MetadataApiKey"
```

## Additional Resources

- [Azure Key Vault documentation](https://docs.microsoft.com/en-us/azure/key-vault/)
- [Azure Functions Key Vault references](https://docs.microsoft.com/en-us/azure/app-service/app-service-key-vault-references)
- [Managed identities for Azure resources](https://docs.microsoft.com/en-us/azure/active-directory/managed-identities-azure-resources/overview)
