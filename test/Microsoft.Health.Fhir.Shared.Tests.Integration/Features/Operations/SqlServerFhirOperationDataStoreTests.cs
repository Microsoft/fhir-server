﻿// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System;
using System.Numerics;
using System.Threading.Tasks;
using Microsoft.Health.Fhir.Tests.Common.FixtureParameters;
using Microsoft.Health.Fhir.Tests.Integration.Persistence;
using Xunit;

namespace Microsoft.Health.Fhir.Tests.Integration.Features.Operations
{
    [Collection(FhirOperationTestConstants.FhirOperationTests)]
    [FhirStorageTestsFixtureArgumentSets(DataStore.SqlServer)]
    public class SqlServerFhirOperationDataStoreTests : IClassFixture<FhirStorageTestsFixture>
    {
        private readonly ISqlServerFhirStorageTestHelper _testHelper;

        public SqlServerFhirOperationDataStoreTests(FhirStorageTestsFixture fixture)
        {
            _testHelper = (SqlServerFhirStorageTestHelper)fixture.TestHelper;
        }

        [Fact]
        public async Task GivenTwoSchemaInitializationMethods_WhenCreatingTwoDatabases_BothSchemasShouldBeEquivalent()
        {
            var snapshotDatabaseName = $"SNAPSHOT_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}_{BigInteger.Abs(new BigInteger(Guid.NewGuid().ToByteArray()))}";
            var diffDatabaseName = $"DIFF_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}_{BigInteger.Abs(new BigInteger(Guid.NewGuid().ToByteArray()))}";

            try
            {
                // Create two databases, one where we apply the the maximum supported version's snapshot SQL schema file
                await _testHelper.CreateAndInitializeDatabase(snapshotDatabaseName, applySqlSchemaSnapshot: true);

                // And one where we apply .diff.sql files to upgrade the schema version to the maximum supported version.
                await _testHelper.CreateAndInitializeDatabase(diffDatabaseName, applySqlSchemaSnapshot: false);

                bool isEqual = _testHelper.CompareDatabaseSchemas(snapshotDatabaseName, diffDatabaseName);
                Assert.True(isEqual);
            }
            finally
            {
                await _testHelper.DeleteDatabase(snapshotDatabaseName);
                await _testHelper.DeleteDatabase(diffDatabaseName);
            }
        }
    }
}
