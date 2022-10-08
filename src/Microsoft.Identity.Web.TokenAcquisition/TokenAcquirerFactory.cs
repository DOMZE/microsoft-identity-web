﻿// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Abstractions;

namespace Microsoft.Identity.Web
{
    /// <summary>
    /// Factory of a token acquirer.
    /// </summary>
    public class TokenAcquirerFactory : ITokenAcquirerFactory
    {
        /// <summary>
        /// Configuration
        /// </summary>
        public IConfiguration Configuration { get; protected set; }

        /// <summary>
        /// Service Provider
        /// </summary>
        public IServiceProvider? ServiceProvider { get; protected set; }

        /// <summary>
        /// Services. Used in the initialization phase.
        /// </summary>
        public ServiceCollection Services { get; protected set; } = new ServiceCollection();

        /// <summary>
        /// Constructor
        /// </summary>
        protected TokenAcquirerFactory()
        {
            Configuration = null!;
        }

        /// <summary>
        /// Get the default instance.
        /// </summary>
        static public T GetDefaultInstance<T>() where T : TokenAcquirerFactory, new()
        {
            T instance;
            if (defaultInstance == null)
            {
                instance = new T();
                instance.ReadConfiguration();
                defaultInstance = instance;
                instance.Services.AddTokenAcquisition();
                instance.Services.AddHttpClient();
            }
            return (defaultInstance as T)!;
        }


        /// <summary>
        /// Get the default instance
        /// </summary>
        /// <returns></returns>
        static public TokenAcquirerFactory GetDefaultInstance()
        {
            TokenAcquirerFactory instance;
            if (defaultInstance == null)
            {
                instance = new TokenAcquirerFactory();
                instance.ReadConfiguration();
                defaultInstance = instance;
                instance.Services.AddTokenAcquisition();
                instance.Services.AddHttpClient();
                instance.Services.AddOptions<MicrosoftAuthenticationOptions>(string.Empty);
            }
            return defaultInstance!;
        }

        /// <summary>
        /// Build the Token acquirer
        /// </summary>
        /// <returns></returns>
        public IServiceProvider Build()
        {
            ServiceProvider = Services.BuildServiceProvider();
            return ServiceProvider;
        }

        /// <summary>
        /// Default instance
        /// </summary>
        private static TokenAcquirerFactory? defaultInstance { get; set; }

        // Move to a derived class?

        private IConfiguration ReadConfiguration()
        {
            if (Configuration == null)
            {
                // Read the configuration from a file
                var builder = new ConfigurationBuilder();
                string basePath = DefineConfiguration(builder);
                builder.SetBasePath(basePath)
                       .AddJsonFile("appsettings.json", optional: true);
                Configuration = builder.Build();
            }
            return Configuration;
        }

        /// <summary>
        /// Adding additional configuration and returns the base path for configuration
        /// files
        /// </summary>
        /// <param name="builder"></param>
        /// <returns>Returns the base path for configuration files</returns>
        protected virtual string DefineConfiguration(IConfigurationBuilder builder)
        {
            Assembly assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
            return Path.GetDirectoryName(assembly!.Location)!;
        }

        readonly IDictionary<string, ITokenAcquirer> _authSchemes = new Dictionary<string, ITokenAcquirer>();

        /// <inheritdoc/>
        public ITokenAcquirer GetTokenAcquirer(string authority, string clientId, IEnumerable<CredentialDescription> clientCredentials, string? region = "TryAutoDetect")
        {
            CheckServiceProviderNotNull();

            ITokenAcquirer? tokenAcquirer;
            // Compute the key
            string key = GetKey(authority, clientId);
            if (!_authSchemes.TryGetValue(key, out tokenAcquirer))
            {
                MicrosoftAuthenticationOptions microsoftAuthenticationOptions = new MicrosoftAuthenticationOptions()
                {
                    ClientId = clientId,
                    Authority = authority,
                    ClientCredentials = clientCredentials,
                    SendX5C = true
                };
                if (region != null)
                {
                    microsoftAuthenticationOptions.AzureRegion = region;
                }

                var optionsMonitor = ServiceProvider!.GetRequiredService<IOptionsMonitor<MergedOptions>>();
                var mergedOptions = optionsMonitor.Get(key);
                MergedOptions.UpdateMergedOptionsFromMicrosoftAuthenticationOptions(microsoftAuthenticationOptions, mergedOptions);
                tokenAcquirer = GetTokenAcquirer(key);
            }
            return tokenAcquirer;
        }

        private void CheckServiceProviderNotNull()
        {
            if (ServiceProvider == null)
            {
                throw new ArgumentOutOfRangeException("You need to call ITokenAcquirerFactory.Build() before using GetTokenAcquirer.");
            }
        }

        /// <inheritdoc/>
        public ITokenAcquirer GetTokenAcquirer(AuthenticationOptions applicationAuthenticationOptions)
        {
            if (applicationAuthenticationOptions is null)
            {
                throw new ArgumentNullException(nameof(applicationAuthenticationOptions));
            }

            CheckServiceProviderNotNull();

            // Compute the Azure region if the option is a MicrosoftAuthenticationOptions.
            MicrosoftAuthenticationOptions? microsoftAuthenticationOptions = applicationAuthenticationOptions as MicrosoftAuthenticationOptions;
            if (microsoftAuthenticationOptions == null)
            {
                microsoftAuthenticationOptions = new MicrosoftAuthenticationOptions
                {
                    AllowWebApiToBeAuthorizedByACL = applicationAuthenticationOptions.AllowWebApiToBeAuthorizedByACL,
                    Audience = applicationAuthenticationOptions.Audience,
                    Audiences = applicationAuthenticationOptions.Audiences,
                    Authority = applicationAuthenticationOptions.Authority,
                    ClientCredentials = applicationAuthenticationOptions.ClientCredentials,
                    ClientId = applicationAuthenticationOptions.ClientId,
                    TokenDecryptionCredentials = applicationAuthenticationOptions.TokenDecryptionCredentials,
                    EnablePiiLogging = applicationAuthenticationOptions.EnablePiiLogging,
                };
            }

            // Compute the key
            ITokenAcquirer? tokenAcquirer;
            string key = GetKey(applicationAuthenticationOptions.Authority, applicationAuthenticationOptions.ClientId);
            if (!_authSchemes.TryGetValue(key, out tokenAcquirer))
            {
                var optionsMonitor = ServiceProvider!.GetRequiredService<IOptionsMonitor<MergedOptions>>();
                var mergedOptions = optionsMonitor.Get(key);
                MergedOptions.UpdateMergedOptionsFromMicrosoftAuthenticationOptions(microsoftAuthenticationOptions, mergedOptions);
                tokenAcquirer = GetTokenAcquirer(key);
            }
            return tokenAcquirer;
        }

        /// <inheritdoc/>
        public ITokenAcquirer GetTokenAcquirer(string authenticationScheme = "")
        {
            CheckServiceProviderNotNull();

            ITokenAcquirer? acquirer;
            if (!_authSchemes.TryGetValue(authenticationScheme, out acquirer))
            {
                var tokenAcquisition = ServiceProvider!.GetRequiredService<ITokenAcquisition>();
                acquirer = new TokenAcquirer(tokenAcquisition, authenticationScheme);
                _authSchemes.Add(authenticationScheme, acquirer);
            }
            return acquirer;
        }

        private static string GetKey(string? authority, string? clientId)
        {
            return $"{authority}{clientId}";
        }
    }
}
