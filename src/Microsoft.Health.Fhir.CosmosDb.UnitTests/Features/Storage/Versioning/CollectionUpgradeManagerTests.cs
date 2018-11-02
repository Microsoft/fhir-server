﻿// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.Documents.Linq;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Health.Fhir.CosmosDb.Configs;
using Microsoft.Health.Fhir.CosmosDb.Features.Storage;
using Microsoft.Health.Fhir.CosmosDb.Features.Storage.Versioning;
using Microsoft.Health.Fhir.CosmosDb.Features.Storage.Versioning.DataMigrations;
using NSubstitute;
using Xunit;
using Task = System.Threading.Tasks.Task;

namespace Microsoft.Health.Fhir.CosmosDb.UnitTests.Features.Storage.Versioning
{
    public class CollectionUpgradeManagerTests
    {
        private readonly CosmosDataStoreConfiguration _cosmosDataStoreConfiguration = new CosmosDataStoreConfiguration
        {
            AllowDatabaseCreation = false,
            CollectionId = "testcollectionid",
            ConnectionMode = ConnectionMode.Direct,
            ConnectionProtocol = Protocol.Https,
            DatabaseId = "testdatabaseid",
            Host = "https://fakehost",
            Key = "ZmFrZWtleQ==",   // "fakekey"
            PreferredLocations = null,
        };

        private readonly CollectionUpgradeManager _manager;
        private readonly IDocumentClient _client;

        public CollectionUpgradeManagerTests()
        {
            var factory = Substitute.For<ICosmosDbDistributedLockFactory>();
            var cosmosDbDistributedLock = Substitute.For<ICosmosDbDistributedLock>();
            var collectionVersionWrappers = Substitute.For<IQueryable<CollectionVersion>, IDocumentQuery<CollectionVersion>>();

            factory.Create(Arg.Any<IDocumentClient>(), Arg.Any<Uri>(), Arg.Any<string>()).Returns(cosmosDbDistributedLock);
            cosmosDbDistributedLock.TryAcquireLock().Returns(true);

            _client = Substitute.For<IDocumentClient>();
            _client.CreateDocumentQuery<CollectionVersion>(Arg.Any<string>(), Arg.Any<SqlQuerySpec>(), Arg.Any<FeedOptions>())
                .Returns(collectionVersionWrappers);

            collectionVersionWrappers.AsDocumentQuery().ExecuteNextAsync<CollectionVersion>().Returns(new FeedResponse<CollectionVersion>(new List<CollectionVersion>()));

            var updaters = new ICollectionUpdater[] { new CollectionSettingsUpdater(NullLogger<CollectionSettingsUpdater>.Instance, _cosmosDataStoreConfiguration, Enumerable.Empty<Migration>()), };
            _manager = new CollectionUpgradeManager(updaters, _cosmosDataStoreConfiguration, factory, NullLogger<CollectionUpgradeManager>.Instance);
        }

        [Fact]
        public async Task GivenACollection_WhenSettingUpCollection_ThenTheCollectionIndexIsUpdated()
        {
            var documentCollection = new DocumentCollection();

            await UpdateCollectionAsync(documentCollection);

            await _client.Received(1).ReplaceDocumentCollectionAsync(Arg.Is(documentCollection));
        }

        [Fact]
        public async Task GivenACollection_WhenSettingUpCollection_ThenTheCollectionVersionWrapperIsSaved()
        {
            var documentCollection = new DocumentCollection();

            await UpdateCollectionAsync(documentCollection);

            await _client.Received(1).UpsertDocumentAsync(Arg.Is(_cosmosDataStoreConfiguration.RelativeCollectionUri), Arg.Is<CollectionVersion>(x => x.SettingsVersion == CollectionUpgradeManager.CollectionSettingsVersion));
        }

        [Fact]
        public async Task GivenACollection_WhenSettingUpCollection_ThenTheCollectionTTLIsSetToNeg1()
        {
            var documentCollection = new DocumentCollection();

            await UpdateCollectionAsync(documentCollection);

            Assert.Equal(-1, documentCollection.DefaultTimeToLive);
        }

        private async Task UpdateCollectionAsync(DocumentCollection documentCollection)
        {
            await _manager.SetupCollectionAsync(_client, documentCollection);
        }
    }
}
