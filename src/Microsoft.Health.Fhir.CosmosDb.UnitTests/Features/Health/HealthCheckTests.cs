﻿// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.Documents;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Health.Fhir.Core.Features.Health;
using Microsoft.Health.Fhir.CosmosDb.Configs;
using Microsoft.Health.Fhir.CosmosDb.Features.Health;
using Microsoft.Health.Fhir.CosmosDb.Features.Storage;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace Microsoft.Health.Fhir.CosmosDb.UnitTests.Features.Health
{
    public class HealthCheckTests
    {
        private readonly IDocumentClient _documentClient = Substitute.For<IDocumentClient>();
        private readonly IDocumentClientTestProvider _testProvider = Substitute.For<IDocumentClientTestProvider>();
        private readonly CosmosDataStoreConfiguration _configuration = new CosmosDataStoreConfiguration { DatabaseId = "mydb", CollectionId = "mycoll" };

        private readonly CosmosHealthCheck _healthCheck;

        public HealthCheckTests()
        {
            _healthCheck = new CosmosHealthCheck(
                new NonDisposingScope(_documentClient),
                _configuration,
                _testProvider,
                NullLogger<CosmosHealthCheck>.Instance);
        }

        [Fact]
        public async Task GivenCosmosDbCanBeQueried_WhenHealthIsChecked_ThenHealthyStateShouldBeReturned()
        {
            HealthCheckResult result = await _healthCheck.CheckAsync();

            Assert.NotNull(result);
            Assert.Equal(HealthState.Healthy, result.HealthState);
        }

        [Fact]
        public async Task GivenCosmosDbCannotBeQueried_WhenHealthIsChecked_ThenUnhealthyStateShouldBeReturned()
        {
            _testProvider.PerformTest(default, default).ThrowsForAnyArgs<HttpRequestException>();
            HealthCheckResult result = await _healthCheck.CheckAsync();

            Assert.NotNull(result);
            Assert.Equal(HealthState.Unhealthy, result.HealthState);
        }
    }
}
