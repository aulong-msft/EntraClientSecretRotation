using Azure.Identity;  
using Azure.Security.KeyVault.Secrets;  
using Microsoft.Graph;
using Microsoft.Graph.Models;
using System;  
using Microsoft.Graph.Applications.Item.AddPassword;
using Microsoft.Identity.Client;
using Microsoft.Graph.Drives.Item.Items.Item.Workbook.Functions.Vdb;
using Microsoft.Graph.Applications.Item.RemovePassword;


class Program  
{  
    // please add these ites to a secure location
    private static string objectId = "ENTER_YOUR_OBJECT_ID_HERE";
    private static string entraSecretName = "ENTER_THE_NAME_FOR_SECRET_HERE";
    private static string keyVaultUrl = "https://ENTER_YOUR_KEYVAULT_NAME.vault.azure.net/";
    private static string newClientSecret = string.Empty;

    static async Task Main(string[] args)  
    {  
        
        var credential = GetCredential();
        Console.WriteLine($"Credential: {credential}");
        Console.WriteLine($"Credential Debug: {credential.GetToken(new Azure.Core.TokenRequestContext(new string[] { "https://vault.azure.net/.default" })).Token}");
    
        var graphClient = new GraphServiceClient(credential);
        await CreateNewSecret(graphClient);
        await AddSecretToKeyVault(credential);
    }

    private static DefaultAzureCredential GetCredential()
    {
        Azure.Identity.DefaultAzureCredentialOptions options = new Azure.Identity.DefaultAzureCredentialOptions  
        {  
            Diagnostics =  
            {  
                IsLoggingEnabled = true,  
                IsTelemetryEnabled = false,  
                LoggedHeaderNames = { "x-ms-request-id" },  
                LoggedQueryParameters = { "api-version" },  
            },  
            ExcludeEnvironmentCredential = false,  
            ExcludeManagedIdentityCredential = false,  
            ExcludeSharedTokenCacheCredential = false,  
            ExcludeVisualStudioCredential = false,  
            ExcludeVisualStudioCodeCredential = false,  
            ExcludeAzureCliCredential = false,  
            ExcludeInteractiveBrowserCredential = true,  
        };  
        return new DefaultAzureCredential(options);  
    }

    private static async Task CreateNewSecret(GraphServiceClient graphClient)
    {
        var requestBody = new AddPasswordPostRequestBody        
        {
            PasswordCredential = new PasswordCredential
            {
                DisplayName = entraSecretName,
                EndDateTime = DateTimeOffset.UtcNow.AddMonths(6), // MSFT recommended rotation time
            },
        };

        try
        {
            var result = await graphClient.Applications[objectId].AddPassword.PostAsync(requestBody);
            Console.WriteLine("New app registration secret created successfully.");

            if (result != null && !string.IsNullOrEmpty(result.SecretText))  
            {  
                newClientSecret = result.SecretText;  
            }  
            else  
            {  
                Console.WriteLine("No secret text returned from Graph API."); 
                return;
            }  
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error creating app registration secret: {ex.Message}");
        }

        Console.WriteLine($"New Client Secret: {newClientSecret}");
    }

    private static async Task AddSecretToKeyVault(DefaultAzureCredential credential)
    {
        var secretClient = new SecretClient(new Uri(keyVaultUrl), credential);  

        KeyVaultSecret updateSecret = new KeyVaultSecret(entraSecretName, newClientSecret);
        Console.WriteLine($"New KeyVault Secret: {updateSecret}");

        SecretProperties secretProperties = updateSecret.Properties;  
        secretProperties.ExpiresOn = DateTimeOffset.UtcNow.AddMonths(6);  

        await secretClient.SetSecretAsync(updateSecret);
        Console.WriteLine("New Client Secret added to Key Vault");
    }
}
