﻿// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System;
using System.Threading;
using System.Threading.Tasks;
using EnsureThat;
using Microsoft.Azure.Documents;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Health.CosmosDb.Configs;
using Microsoft.Health.CosmosDb.Features.Storage;
using Microsoft.Health.Extensions.DependencyInjection;
using Microsoft.Health.Fhir.Core.Features.Health;

namespace Microsoft.Health.Fhir.CosmosDb.Features.Health
{
    /// <summary>
    /// Checks for the FHIR service health.
    /// </summary>
    public class FhirCosmosHealthCheck : IHealthCheck
    {
        private readonly IScoped<IDocumentClient> _documentClient;
        private readonly CosmosDataStoreConfiguration _configuration;
        private readonly CosmosCollectionConfiguration _collectionConfiguration;
        private readonly IDocumentClientTestProvider _testProvider;
        private readonly ILogger<FhirCosmosHealthCheck> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="FhirCosmosHealthCheck"/> class.
        /// </summary>
        /// <param name="documentClient">The document client factory/</param>
        /// <param name="configuration">The CosmosDB configuration.</param>
        /// <param name="namedCosmosCollectionConfigurationAccessor">The IOptions accessor to get a named version.</param>
        /// <param name="testProvider">The test provider</param>
        /// <param name="logger">The logger.</param>
        public FhirCosmosHealthCheck(
            IScoped<IDocumentClient> documentClient,
            CosmosDataStoreConfiguration configuration,
            IOptionsSnapshot<CosmosCollectionConfiguration> namedCosmosCollectionConfigurationAccessor,
            IDocumentClientTestProvider testProvider,
            ILogger<FhirCosmosHealthCheck> logger)
        {
            EnsureArg.IsNotNull(documentClient, nameof(documentClient));
            EnsureArg.IsNotNull(configuration, nameof(configuration));
            EnsureArg.IsNotNull(namedCosmosCollectionConfigurationAccessor, nameof(namedCosmosCollectionConfigurationAccessor));
            EnsureArg.IsNotNull(testProvider, nameof(testProvider));
            EnsureArg.IsNotNull(logger, nameof(logger));

            _documentClient = documentClient;

            EnsureArg.IsNotNull(_documentClient, optsFn: options => options.WithMessage("Factory returned null."));

            _configuration = configuration;
            _collectionConfiguration = namedCosmosCollectionConfigurationAccessor.Get(Constants.CollectionConfigurationName);
            _testProvider = testProvider;
            _logger = logger;
        }

        public async Task<HealthCheckResult> CheckAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            try
            {
                // Make a non-invasive query to make sure we can reach the data store.

                await _testProvider.PerformTest(_documentClient.Value, _configuration, _collectionConfiguration);

                return HealthCheckResult.Healthy("Successfully connected to the data store.");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to connect to the data store.");

                return HealthCheckResult.Unhealthy("Failed to connect to the data store.");
            }
        }
    }
}
