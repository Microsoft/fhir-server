﻿// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Microsoft.Health.Core.Features.Security;
using Microsoft.Health.Fhir.Core.Configs;
using Microsoft.Health.Fhir.Core.Features.Operations;
using Microsoft.Health.Fhir.Core.Features.Operations.Export;
using Microsoft.Health.Fhir.Core.Features.Operations.Export.Models;
using Microsoft.Health.Fhir.Core.Features.Persistence;
using Microsoft.Health.Fhir.Core.Features.Security.Authorization;
using Microsoft.Health.Fhir.Core.Messages.Export;
using Microsoft.Health.Fhir.Core.Models;
using Microsoft.Health.Fhir.Tests.Common.FixtureParameters;
using Microsoft.Health.Fhir.Tests.Integration.Persistence;
using NSubstitute;
using Xunit;

namespace Microsoft.Health.Fhir.Tests.Integration.Features.Operations.Export
{
    [Collection(FhirOperationTestConstants.FhirOperationTests)]
    [FhirStorageTestsFixtureArgumentSets(DataStore.All)]
    public class CreateExportRequestHandlerTests : IClassFixture<FhirStorageTestsFixture>, IAsyncLifetime
    {
        private static readonly Uri RequestUrl = new Uri("https://localhost/$export");
        private static readonly PartialDateTime SinceParameter = new PartialDateTime(DateTimeOffset.UtcNow);
        private static readonly Uri RequestUrlWithSince = new Uri($"https://localhost/$export?_since={SinceParameter}");

        private readonly MockClaimsExtractor _claimsExtractor = new MockClaimsExtractor();
        private readonly IFhirOperationDataStore _fhirOperationDataStore;
        private readonly IFhirStorageTestHelper _fhirStorageTestHelper;

        private CreateExportRequestHandler _createExportRequestHandler;
        private ExportJobConfiguration _exportJobConfiguration;

        private readonly CancellationToken _cancellationToken = new CancellationTokenSource().Token;

        public CreateExportRequestHandlerTests(FhirStorageTestsFixture fixture)
        {
            _fhirOperationDataStore = AddListener(fixture.OperationDataStore);
            _fhirStorageTestHelper = fixture.TestHelper;

            _exportJobConfiguration = new ExportJobConfiguration();
            _exportJobConfiguration.Formats = new List<ExportJobFormatConfiguration>();
            _exportJobConfiguration.Formats.Add(new ExportJobFormatConfiguration()
            {
                Name = "test",
                Format = ExportFormatTags.ResourceName,
            });

            IOptions<ExportJobConfiguration> optionsExportConfig = Substitute.For<IOptions<ExportJobConfiguration>>();
            optionsExportConfig.Value.Returns(_exportJobConfiguration);

            _createExportRequestHandler = new CreateExportRequestHandler(_claimsExtractor, _fhirOperationDataStore, DisabledFhirAuthorizationService.Instance, optionsExportConfig);
        }

        public static IEnumerable<object[]> ExportUriForSameJobs
        {
            get
            {
                return new[]
                {
                    new object[] { RequestUrl, null },
                    new object[] { RequestUrlWithSince, SinceParameter },
                };
            }
        }

        public static IEnumerable<object[]> ExportUriForDifferentJobs
        {
            get
            {
                return new[]
                {
                    new object[] { RequestUrl, null, RequestUrlWithSince, SinceParameter },
                    new object[] { RequestUrl, null, new Uri("http://localhost/test"), null },
                    new object[] { RequestUrlWithSince, SinceParameter, new Uri("https://localhost/$export?_since=2020-01-01"), PartialDateTime.Parse("2020-01-01") },
                };
            }
        }

        public Task InitializeAsync()
        {
            return _fhirStorageTestHelper.DeleteAllExportJobRecordsAsync(_cancellationToken);
        }

        public Task DisposeAsync()
        {
            return Task.CompletedTask;
        }

        [Theory]
        [MemberData(nameof(ExportUriForSameJobs))]
        public async Task GivenThereIsNoMatchingJob_WhenCreatingAnExportJob_ThenNewJobShouldBeCreated(Uri requestUrl, PartialDateTime since)
        {
            var request = new CreateExportRequest(requestUrl, ExportJobType.All, since: since);

            CreateExportResponse response = await _createExportRequestHandler.Handle(request, _cancellationToken);

            Assert.NotNull(response);
            Assert.False(string.IsNullOrWhiteSpace(response.JobId));
        }

        [MemberData(nameof(ExportUriForSameJobs))]
        [Theory]
        public async Task GivenThereIsAMatchingJob_WhenCreatingAnExportJob_ThenExistingJobShouldBeReturned(Uri requestUri, PartialDateTime since)
        {
            var request = new CreateExportRequest(requestUri, ExportJobType.All, since: since);

            CreateExportResponse response = await _createExportRequestHandler.Handle(request, _cancellationToken);

            var newRequest = new CreateExportRequest(requestUri, ExportJobType.All, since: since);

            CreateExportResponse newResponse = await _createExportRequestHandler.Handle(request, _cancellationToken);

            Assert.NotNull(newResponse);
            Assert.Equal(response.JobId, newResponse.JobId);
        }

        [MemberData(nameof(ExportUriForDifferentJobs))]
        [Theory]
        public async Task GivenDifferentRequestUrl_WhenCreatingAnExportJob_ThenNewJobShouldBeCreated(Uri requestUri, PartialDateTime since, Uri newRequestUri, PartialDateTime newSince)
        {
            var request = new CreateExportRequest(requestUri, ExportJobType.All, since: since);

            CreateExportResponse response = await _createExportRequestHandler.Handle(request, _cancellationToken);

            var newRequest = new CreateExportRequest(newRequestUri, ExportJobType.All, since: newSince);

            CreateExportResponse newResponse = await _createExportRequestHandler.Handle(newRequest, _cancellationToken);

            Assert.NotNull(newResponse);
            Assert.NotEqual(response.JobId, newResponse.JobId);
        }

        [Fact]
        public async Task GivenDifferentRequestor_WhenCreatingAnExportJob_ThenNewJobShouldBeCreated()
        {
            _claimsExtractor.ExtractImpl = () => new[] { KeyValuePair.Create("oid", "user1") };

            var request = new CreateExportRequest(RequestUrl, ExportJobType.All);

            CreateExportResponse response = await _createExportRequestHandler.Handle(request, _cancellationToken);

            _claimsExtractor.ExtractImpl = () => new[] { KeyValuePair.Create("oid", "user2") };

            var newRequest = new CreateExportRequest(RequestUrl, ExportJobType.All);

            CreateExportResponse newResponse = await _createExportRequestHandler.Handle(newRequest, _cancellationToken);

            Assert.NotNull(newResponse);
            Assert.NotEqual(response.JobId, newResponse.JobId);
        }

        [Fact]
        public async Task GivenThereIsAMatchingJob_WhenRequestorClaimsInDifferentOrder_ThenExistingJobShouldBeReturned()
        {
            var claim1 = KeyValuePair.Create("oid", "user1");
            var claim2 = KeyValuePair.Create("iss", "http://localhost/authority");

            _claimsExtractor.ExtractImpl = () => new[] { claim1, claim2 };

            var request = new CreateExportRequest(RequestUrl, ExportJobType.All);

            CreateExportResponse response = await _createExportRequestHandler.Handle(request, _cancellationToken);

            _claimsExtractor.ExtractImpl = () => new[] { claim2, claim1 };

            var newRequest = new CreateExportRequest(RequestUrl, ExportJobType.All);

            CreateExportResponse newResponse = await _createExportRequestHandler.Handle(newRequest, _cancellationToken);

            Assert.NotNull(newResponse);
            Assert.Equal(response.JobId, newResponse.JobId);
        }

        [Theory]
        [InlineData("test1", ExportFormatTags.ResourceName)]
        [InlineData(null, ExportFormatTags.Id)]
        public async Task GivenARequestWithDifferentFormatNames_WhenConverted_ThenTheProperFormatStringIsReturned(string formatName, string expectedFormat)
        {
            _exportJobConfiguration.Formats.Clear();
            _exportJobConfiguration.Formats.Add(new ExportJobFormatConfiguration()
            {
                Name = "test1",
                Format = ExportFormatTags.ResourceName,
            });
            _exportJobConfiguration.Formats.Add(new ExportJobFormatConfiguration()
            {
                Name = "test2",
                Format = ExportFormatTags.Id,
                Default = true,
            });
            _exportJobConfiguration.Formats.Add(new ExportJobFormatConfiguration()
            {
                Name = "test3",
                Format = ExportFormatTags.Timestamp,
            });

            ExportJobRecord actualRecord = null;
            await _fhirOperationDataStore.CreateExportJobAsync(
                Arg.Do<ExportJobRecord>(record =>
            {
                actualRecord = record;
            }), Arg.Any<CancellationToken>());

            var request = new CreateExportRequest(RequestUrl, ExportJobType.All, null, formatName: formatName);
            CreateExportResponse response = await _createExportRequestHandler.Handle(request, _cancellationToken);

            Assert.Equal(expectedFormat, actualRecord.ExportFormat);
        }

        [Theory]
        [InlineData(false, ExportFormatTags.ResourceName)]
        [InlineData(true, ExportFormatTags.Timestamp + "-" + ExportFormatTags.Id + "/" + ExportFormatTags.ResourceName)]
        public async Task GivenARequest_WhenNoFormatsAreSet_ThenHardcodedDefaultIsReturned(bool containerSpecified, string expectedFormat)
        {
            _exportJobConfiguration.Formats.Clear();

            ExportJobRecord actualRecord = null;
            await _fhirOperationDataStore.CreateExportJobAsync(
                Arg.Do<ExportJobRecord>(record =>
                {
                    actualRecord = record;
                }), Arg.Any<CancellationToken>());

            var request = new CreateExportRequest(RequestUrl, ExportJobType.All, containerName: containerSpecified ? "test" : null);
            CreateExportResponse response = await _createExportRequestHandler.Handle(request, _cancellationToken);

            Assert.Equal(expectedFormat, actualRecord.ExportFormat);
        }

        [Fact]
        public async Task GivenARequestWithANonexistantFormatName_WhenConverted_ThenABadRequestIsReturned()
        {
            var request = new CreateExportRequest(RequestUrl, ExportJobType.All, formatName: "invalid");
            await Assert.ThrowsAsync<BadRequestException>(() => _createExportRequestHandler.Handle(request, _cancellationToken));
        }

        /// <summary>
        /// Adds a listener to an object so that it can be spied on.
        /// This allows objects passed in through the fixture to have method calls tracked and arguments recorded.
        /// All the calls go through the spy to the underlying object.
        /// </summary>
        /// <typeparam name="T">The type of object passed</typeparam>
        /// <param name="baseObject">The object to add a listener to</param>
        /// <returns>The object wrapped in a spy</returns>
        private T AddListener<T>(T baseObject)
        {
            Type type = typeof(T);
            T spy = (T)Substitute.For(new[] { typeof(T) }, new object[0]);
            foreach (var method in typeof(T).GetMethods())
            {
                object[] inputArgs = new object[method.GetParameters().Length];
                for (int index = 0; index < inputArgs.Length; index++)
                {
                    inputArgs[index] = default;
                }

                type.InvokeMember(method.Name, System.Reflection.BindingFlags.InvokeMethod, null, spy, inputArgs).ReturnsForAnyArgs(args =>
                {
                    return type.InvokeMember(method.Name, System.Reflection.BindingFlags.InvokeMethod, null, baseObject, args.Args());
                });
            }

            return spy;
        }

        private class MockClaimsExtractor : IClaimsExtractor
        {
            public Func<IReadOnlyCollection<KeyValuePair<string, string>>> ExtractImpl { get; set; }

            public IReadOnlyCollection<KeyValuePair<string, string>> Extract()
            {
                if (ExtractImpl == null)
                {
                    return Array.Empty<KeyValuePair<string, string>>();
                }

                return ExtractImpl();
            }
        }
    }
}
