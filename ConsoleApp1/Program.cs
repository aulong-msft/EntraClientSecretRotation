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
    private static readonly string keyVaultUrl = "https://keyvaultforentra.vault.azure.net/";   
    private static readonly string objectId = "a726f66f-1002-421c-9845-440054f82ec7";
    private static readonly string entraSecretName = "entraSecret";
    private static string newClientSecret = string.Empty;

    static async Task Main(string[] args)  
    {  
        var credential = GetCredential();
        Console.WriteLine($"Credential: {credential}");
        Console.WriteLine($"Credential Debug: {credential.GetToken(new Azure.Core.TokenRequestContext(new string[] { "https://vault.azure.net/.default" })).Token}");

        var graphClient = new GraphServiceClient(credential);
        await DeleteOldestPassword(graphClient);
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

    private static async Task DeleteOldestPassword(GraphServiceClient graphClient)
    {
        try  
        {  
            var passwordCredentials = await graphClient.Applications[objectId].GetAsync();  
            if (passwordCredentials == null || passwordCredentials.PasswordCredentials == null || passwordCredentials.PasswordCredentials.Count == 0)  
            {  
                Console.WriteLine("No password credentials found.");  
                return;  
            }
            var soonestExpiringCredential = passwordCredentials.PasswordCredentials.OrderBy(pc => pc.EndDateTime).FirstOrDefault();  
        
            if (soonestExpiringCredential != null)  
            {  
                Console.WriteLine($"Deleting Secret ID: {soonestExpiringCredential.KeyId}");  
                Console.WriteLine($"Display Name: {soonestExpiringCredential.DisplayName}");  
                Console.WriteLine($"End Date: {soonestExpiringCredential.EndDateTime}");  
        
                var requestBody = new RemovePasswordPostRequestBody  
                {  
                    KeyId = soonestExpiringCredential.KeyId.Value,  
                };  
                
                await graphClient.Applications[objectId].RemovePassword.PostAsync(requestBody);  
                Console.WriteLine("Secret deleted successfully.");  
            }  
            else  
            {  
                Console.WriteLine("No password credentials found.");  
            }  
        }  
        catch (Exception ex)  
        {  
            Console.WriteLine($"Error retrieving or deleting secrets: {ex.Message}");  
        }  
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
