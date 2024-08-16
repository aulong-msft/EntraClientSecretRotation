using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Applications.Item.AddPassword;
using System.Text.Json;

namespace Company.Function
{
    public class MyEventType
    {
        public string Id { get; set; } = string.Empty;
        public string Topic { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string EventType { get; set; } = string.Empty;
        public DateTime EventTime { get; set; }
        public IDictionary<string, object> Data { get; set; } = new Dictionary<string, object>();
    }

    public class EventGridTriggerFunction
    {
        private readonly ILogger<EventGridTriggerFunction> _logger;
        private static string newClientSecret = string.Empty;

        public EventGridTriggerFunction(ILogger<EventGridTriggerFunction> logger)
        {
            _logger = logger;
        }

        [Function("EventGridTrigger")]
        public async Task<IActionResult> Run([EventGridTrigger] BinaryData[] input, FunctionContext context)
        {
            _logger.LogInformation("C# EventGrid trigger function processed a request.");

            foreach (var eventData in input)
            {
                var myEvent = JsonSerializer.Deserialize<MyEventType>(eventData.ToString());
                if (myEvent == null)
                {
                    _logger.LogError("Failed to deserialize event data.");
                    continue;
                }

                _logger.LogInformation(myEvent.EventType);
                _logger.LogInformation(JsonSerializer.Serialize(myEvent.Data));

                // Retrieve the environment variables
                string secretName = Environment.GetEnvironmentVariable("SecretName");
                string objectId = Environment.GetEnvironmentVariable("EntraObjectID");
                string keyVaultUri = Environment.GetEnvironmentVariable("KeyVaultURI");

                // Ensure the environment variables are not null or empty
                if (string.IsNullOrEmpty(secretName))
                {
                    throw new InvalidOperationException("SecretName environment variable is not set.");
                }

                if (string.IsNullOrEmpty(objectId))
                {
                    throw new InvalidOperationException("objectId environment variable is not set.");
                }

                if (string.IsNullOrEmpty(keyVaultUri))
                {
                    throw new InvalidOperationException("KeyVaultUri environment variable is not set.");
                }

                if (myEvent.EventType == "Microsoft.KeyVault.SecretNearExpiry" || myEvent.EventType == "Microsoft.KeyVault.SecretExpired")
                {
                    try
                    {
                        var credential = new DefaultAzureCredential();
                        var graphClient = new GraphServiceClient(credential);
                        await CreateNewSecret(graphClient, secretName, objectId);

                        if (!string.IsNullOrEmpty(newClientSecret))
                        {
                            await AddSecretToKeyVault(credential, keyVaultUri);
                            _logger.LogInformation("Secret rotation completed successfully.");
                        }
                        else
                        {
                            _logger.LogError("Secret creation failed, nothing added to Key Vault.");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Secret rotation failed: {ex.Message}");
                    }
                }
            }

            return new OkResult();
        }

        private async Task CreateNewSecret(GraphServiceClient graphClient, string secretName, string objectId)
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
                var result = await graphClient.Applications[objectId].AddPassword.PostAsync(requestBody);
                if (result != null && !string.IsNullOrEmpty(result.SecretText))
                {
                    newClientSecret = result.SecretText;
                }
                else
                {
                    _logger.LogError("No secret text returned from Graph API.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error creating app registration secret: {ex.Message}");
            }
        }

        private async Task AddSecretToKeyVault(DefaultAzureCredential credential, string keyVaultUri)
        {
            var secretClient = new SecretClient(new Uri(keyVaultUri), credential);

            var updateSecret = new KeyVaultSecret("entraSecret", newClientSecret)
            {
                Properties = { ExpiresOn = DateTimeOffset.UtcNow.AddMonths(6) }
            };

            await secretClient.SetSecretAsync(updateSecret);
            _logger.LogInformation("New Client Secret added to Key Vault");
        }
    }
}
