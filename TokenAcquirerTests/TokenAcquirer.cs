// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Graph;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.TokenCacheProviders.InMemory;
using Xunit;

namespace TokenAcquirerTests
{
#if !FROM_GITHUB_ACTION
    public class TokenAcquirer
    {
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task AcquireToken_ClientCredentialsAsync(bool withMSIdentityOptions)
        {
            TokenAcquirerFactory tokenAcquirerFactory = TokenAcquirerFactory.GetDefaultInstance();
            IServiceCollection services = tokenAcquirerFactory.Services;

            if (withMSIdentityOptions)
            {
                services.Configure<MicrosoftIdentityOptions>(option =>
                {
                    option.Instance = "https://login.microsoftonline.com/";
                    option.TenantId = "msidentitysamplestesting.onmicrosoft.com";
                    option.ClientId = "6af093f3-b445-4b7a-beae-046864468ad6";
                    option.ClientCertificates = new[] { new CertificateDescription
                {
                    SourceType = CertificateSource.KeyVault,
                    KeyVaultUrl = "https://webappsapistests.vault.azure.net",
                    KeyVaultCertificateName = "Self-Signed-5-5-22"
                } };
                });
            }
            else
            {
                services.Configure<MicrosoftAuthenticationOptions>(option =>
                {
                    option.Instance = "https://login.microsoftonline.com/";
                    option.TenantId = "msidentitysamplestesting.onmicrosoft.com";
                    option.ClientId = "6af093f3-b445-4b7a-beae-046864468ad6";
                    option.ClientCredentials = new[] { new CertificateDescription
                {
                    SourceType = CertificateSource.KeyVault,
                    KeyVaultUrl = "https://webappsapistests.vault.azure.net",
                    KeyVaultCertificateName = "Self-Signed-5-5-22"
                } };
                });
            }

            services.AddInMemoryTokenCaches();
            services.AddMicrosoftGraph();
            var serviceProvider = tokenAcquirerFactory.Build();
            GraphServiceClient graphServiceClient = serviceProvider.GetRequiredService<GraphServiceClient>();
            var users = await graphServiceClient.Users
                .Request()
                .WithAppOnly()
                //     .WithAuthenticationOptions(options => options.ProtocolScheme = "Pop")
                .GetAsync();
            Assert.Equal(50, users.Count);

            // Get the token acquisition service
            ITokenAcquirer tokenAcquirer = serviceProvider.GetRequiredService<ITokenAcquirer>();
            var result = await tokenAcquirer.GetTokenForAppAsync("https://graph.microsoft.com/.default");
            Assert.NotNull(result.AccessToken);
        }

        [Fact]
        public async Task AcquireTokenWithPop_ClientCredentialsAsync()
        {
            TokenAcquirerFactory tokenAcquirerFactory = TokenAcquirerFactory.GetDefaultInstance();
            IServiceCollection services = tokenAcquirerFactory.Services;

                services.Configure<MicrosoftAuthenticationOptions>(option =>
                {
                    option.Instance = "https://login.microsoftonline.com/";
                    option.TenantId = "msidentitysamplestesting.onmicrosoft.com";
                    option.ClientId = "6af093f3-b445-4b7a-beae-046864468ad6";
                    option.ClientCredentials = new[] { new CertificateDescription
                {
                    SourceType = CertificateSource.KeyVault,
                    KeyVaultUrl = "https://webappsapistests.vault.azure.net",
                    KeyVaultCertificateName = "Self-Signed-5-5-22"
                } };
                });

            services.AddInMemoryTokenCaches();
            var serviceProvider = tokenAcquirerFactory.Build();
            var options = serviceProvider.GetRequiredService<IOptions<MicrosoftAuthenticationOptions>>().Value;
            var cert = options.ClientCredentials.First().Certificate;
            
            // Get the token acquisition service
            ITokenAcquirer tokenAcquirer = serviceProvider.GetRequiredService<ITokenAcquirer>();
            var result = await tokenAcquirer.GetTokenForAppAsync("https://graph.microsoft.com/.default", new TokenAcquisitionOptions() { PopPublicKey = cert?.GetPublicKeyString() });
            Assert.NotNull(result.AccessToken);
        }
    }
#endif //FROM_GITHUB_ACTION
}
