using Azure.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Microsoft.Identity.Client;
using System;
using System.Threading.Tasks;

namespace SnowflakeExternalFunctions.HelperClasses.Authentication
{
    public class Authenticate
    {

        private AuthenticationConfig config;

        private IConfidentialClientApplication app;
        private string[] scopes;
        private ILogger _logger;

        public Authenticate(ILogger log)
        {
            config = new AuthenticationConfig();

            app = ConfidentialClientApplicationBuilder.Create(config.ClientId)
              .WithClientSecret(config.ClientSecret)
              .WithAuthority(new Uri(config.Authority))
              .Build();

            // The client credentials flow requires that you request the
            // /.default scope, and preconfigure your permissions on the
            // app registration in Azure. An administrator must grant consent
            // to those permissions beforehand.
            var AdProtectedApiScopes = Environment.GetEnvironmentVariable("ApiScope", EnvironmentVariableTarget.Process);

            if (AdProtectedApiScopes != null)
            {
                scopes = new string[] { AdProtectedApiScopes };
            }
            else
            {

                scopes = new string[] { "https://graph.microsoft.com/.default" };
            }

            _logger = log;
        }


        public GraphServiceClient GetClient()
        {
            try
            {


                // Multi-tenant apps can use "common",
                // single-tenant apps must use the tenant ID from the Azure portal
                var tenantId = config.Tenant;

                // Values from app registration
                var clientId = config.ClientId;
                var clientSecret = config.ClientSecret;

                // using Azure.Identity;
                var options = new TokenCredentialOptions
                {
                    AuthorityHost = AzureAuthorityHosts.AzurePublicCloud
                };

                // https://docs.microsoft.com/dotnet/api/azure.identity.clientsecretcredential
                var clientSecretCredential = new ClientSecretCredential(
                    tenantId, clientId, clientSecret, options);

                return new GraphServiceClient(clientSecretCredential, scopes);
            }
            catch (Exception ex)
            {

                _logger.LogError(ex, ex.Message);
                throw;
            }

        }


        public async Task<AuthenticationResult> GetAuthenticationResultAsync()
        {
            AuthenticationResult result = null;
            try
            {
                result = await app.AcquireTokenForClient(scopes)
                                 .ExecuteAsync();

                _logger.LogInformation("Auth result received");
                return result;


            }
            catch (MsalUiRequiredException ex)
            {
                // The application doesn't have sufficient permissions.
                // - Did you declare enough app permissions during app creation?
                // - Did the tenant admin grant permissions to the application?
                _logger.LogError(ex, ex.Message);
                throw ex;
            }
            catch (MsalServiceException ex) when (ex.Message.Contains("AADSTS70011"))
            {
                // Invalid scope. The scope has to be in the form "https://resourceurl/.default"
                // Mitigation: Change the scope to be as expected.
                _logger.LogError(ex, ex.Message);

                throw ex;
            }

            throw new Exception("Authentication FAILED!");
        }
    }
}
