# Azure Key Vault and Microsoft Graph Integration

This project demonstrates how to interact with Azure Key Vault and Microsoft Graph to manage secrets and application registration client secrets.

##Architecture

This project demonstrates how to interact with Azure Key Vault and Microsoft Graph to manage secrets and application registration client secrets.

## Architecture
![Architecture Diagram](https://github.com/aulong-msft/EntraClientSecretRotation/blob/main/pictures/architecture.jpg)

## Overview
This program is designed to manage Entra Application Registration secrets using the Azure KeyVault service. It first retrieves the application's credentials, then identifies and deletes the oldest password from the application's credentials using Microsoft's Graph API. After deleting the old password, it generates a new secret for the application and adds this newly created secret to the Azure KeyVault. This helps ensure that the application's secrets are always up-to-date and securely stored, providing a robust solution for managing application secrets in Azure.

## Scenario
Rotating client secrets for an Entra application registration can present a significant challenge due to the multiple steps involved. These include deleting the old secret, generating a new one, and ensuring its secure update in Azure KeyVault. The process becomes more complex when considering the dependencies of various applications and services on this secret. They all need to be updated simultaneously to avoid disruptions. Moreover, maintaining robust security practices throughout this process is crucial to prevent potential secret leaks. The necessity for appropriate access permissions and the implementation of automation to ensure regular secret rotations further complicates the task. This program addresses these challenges by automating the process of rotating the client secrets, enhancing the security and efficiency of secret management in Azure.

## Set up
- .NET SDK installed
- Azure subscription
- Azure Function Event Grid Triggered Function
- Azure Key Vault
- Azure Key Vault Event Setup for Exipred and Near Expired Secrets
- App Registration in Entra ID with necessary permissions
- Managed identity with access to Key Vault and Microsoft Graph

### Set up an Event Grid Triggered Azure Function

### Set up the Key Vault Event Grid Trigger

### Create a System Assigned Managed Identity in the Function

### Assign Azure + Entra RBAC to the System Assigned Managed Identity



## Considerations

### Key Vault Secrets Versions Lifecycle
In Azure Key Vault, both current and older versions of secrets remain active and accessible until they either expire or are explicitly disabled. This means that any version of a secret can be used by your applications as long as it is within its validity period and hasn’t been disabled. This approach ensures that you can seamlessly transition between different versions of a secret without disrupting your applications, providing flexibility and continuity in managing sensitive information.

This program does not delete or disable secrets or older versions of secrets in the Key Vault, management of this should be considered.

### Retrieve multiple versions from Key Vault to reduce downtime
To implement the retrieval of the two latest versions of a secret from Azure KeyVault, you will need to utilize the `list_properties_of_secret_versions` method provided by the Azure SDK. This method returns an iterator over the versions of a secret in your Azure KeyVault. By iterating over the returned object and stopping after the first two iterations, you can obtain the two latest versions of the secret. Note that these versions are returned in the order they were created, with the latest version first. You will need to integrate this logic into your existing KeyVault retrieval code, ensuring that your application is capable of fetching and potentially using both of these versions of the secret. Please ensure that the Azure account you use has the necessary permissions to list and get secrets from your Azure Key Vault. This approach allows for greater flexibility and control over your application's secret management.

### Logging 
To view the best possible logs for this sample, it is highly encouraged to enable the Key Vault Diagnostic settings to show "audit","allLogs", and "AllMetrics" and send them to a log analtics workspace to view the Key Vault events.

This sample contains the latest tracing enabled in the Program.cs file, this code will allow you to see the logging information from the Function in the Function's trace logs.

## Limitations

### Testing Key Vault for Expired and Near Expired Secrets
Testing this feature can come with long wait times, on average the "Microsoft.KeyVault.SecretNearExpiry" and "Microsoft.KeyVault.SecretExpired" can come to the Event Grid on average around ~45 mins to 1.3 hours. For testing purposes "Microsoft.KeyVault.SecretNewVersionCreated" event comes instantanously when creating a new secret to the Key Vault, the code can be modified to accept this event to test that everything is configured properly. 

### App Registration
 If you have an app registration configured to support personal account login, you can only create two client secrets at most. If your application only supports work account login, there will be no limit to the number of client secrets created. With the scenatio of personal account, you need to add custom logic to delete the oldest secret.
