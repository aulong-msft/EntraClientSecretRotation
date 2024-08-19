using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Applications.Item.AddPassword;

namespace function
{
    public class RotationFunc
    {
        private readonly ILogger<RotationFunc> _logger;
        private static string newClientSecret = string.Empty;

        public RotationFunc(ILogger<RotationFunc> logger)
        {
            _logger = logger;
        }

        [Function("RotationFunc")]
        public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequest req)
        {
            _logger.LogInformation("C# HTTP trigger function processed a request.");

            string keyVaultUri = Environment.GetEnvironmentVariable("KeyVaultURI");
            string appRegistrationObjectId = Environment.GetEnvironmentVariable("EntraObjectID");
            string secretName = Environment.GetEnvironmentVariable("SecretName");

            var credential = GetCredential();
            var graphClient = new GraphServiceClient(credential);
            await CreateNewSecret(graphClient, appRegistrationObjectId, secretName);
            await AddSecretToKeyVault(credential, keyVaultUri, secretName);

            return new OkObjectResult("Welcome to Azure Functions!");
        }

        private DefaultAzureCredential GetCredential()
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

        private async Task CreateNewSecret(GraphServiceClient graphClient, string appRegistrationObjectId, string secretName)
        {
            var requestBody = new AddPasswordPostRequestBody
            {
                PasswordCredential = new PasswordCredential
                {
                    DisplayName = secretName,
                    EndDateTime = DateTimeOffset.UtcNow.AddMonths(6), // MSFT recommended rotation time
                },
            };

            try
            {
                var result = await graphClient.Applications[appRegistrationObjectId].AddPassword.PostAsync(requestBody);
                if (result != null && !string.IsNullOrEmpty(result.SecretText))
                {
                    newClientSecret = result.SecretText;
                }
                else
                {
                    _logger.LogError("No secret text returned from Graph API.");
                    throw new Exception("No secret text returned from Graph API.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error creating app registration secret: {ex.Message}");
                throw;
            }
        }

        private async Task AddSecretToKeyVault(DefaultAzureCredential credential, string keyVaultUri, string secretName)
        {
            if (string.IsNullOrEmpty(newClientSecret))
            {
                _logger.LogError("New client secret is empty. Cannot add to Key Vault.");
                throw new Exception("New client secret is empty. Cannot add to Key Vault.");
            }

            var secretClient = new SecretClient(new Uri(keyVaultUri), credential);

            KeyVaultSecret updateSecret = new KeyVaultSecret(secretName, newClientSecret);
            SecretProperties secretProperties = updateSecret.Properties;
            secretProperties.ExpiresOn = DateTimeOffset.UtcNow.AddMonths(6);

            try
            {
                await secretClient.SetSecretAsync(updateSecret);
                _logger.LogInformation("New Client Secret added to Key Vault");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error adding secret to Key Vault: {ex.Message}");
                throw;
            }
        }
    }
}
