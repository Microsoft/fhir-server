﻿// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System.Collections.Generic;
using System.Linq;
using System.Net;
using Hl7.Fhir.Model;
using Microsoft.Health.Fhir.Core.Extensions;
using Microsoft.Health.Fhir.Tests.Common;
using Microsoft.Health.Fhir.Tests.Common.FixtureParameters;
using Microsoft.Health.Fhir.Tests.E2E.Common;
using Xunit;
using static Hl7.Fhir.Model.OperationOutcome;
using Task = System.Threading.Tasks.Task;

namespace Microsoft.Health.Fhir.Tests.E2E.Rest
{
    [Trait(Traits.Category, Categories.Batch)]
    [HttpIntegrationFixtureArgumentSets(DataStore.All, Format.All)]
    public class BatchTests : IClassFixture<HttpIntegrationTestFixture>
    {
        private readonly Dictionary<HttpStatusCode, string> statusCodeMap = new Dictionary<HttpStatusCode, string>()
        {
            { HttpStatusCode.NoContent, "204" },
            { HttpStatusCode.NotFound, "404" },
            { HttpStatusCode.OK, "200" },
            { HttpStatusCode.PreconditionFailed, "412" },
            { HttpStatusCode.Created, "201" },
            { HttpStatusCode.Forbidden, "403" },
        };

        public BatchTests(HttpIntegrationTestFixture fixture)
        {
            Client = fixture.FhirClient;
        }

        protected FhirClient Client { get; set; }

        [Fact]
        [Trait(Traits.Priority, Priority.One)]
        public async Task WhenSubmittingABatch_GivenAProperBundle_ThenSuccessIsReturned()
        {
            Resource requestBundle = Samples.GetDefaultBatch().ToPoco<Bundle>();

            FhirResponse<Bundle> fhirResponse = await Client.PostBundleAsync(requestBundle);
            Assert.NotNull(fhirResponse);
            Assert.Equal(HttpStatusCode.OK, fhirResponse.StatusCode);
            ValidateResourceStatusCode(fhirResponse.Resource);
        }

        [Fact]
        [Trait(Traits.Priority, Priority.One)]
        public async Task WhenSubmittingABatch_GivenAProperBundleWithFewFailedRequests_ThenSuccessIsReturnedAndOperationOutcomesForFailedRequests()
        {
            Resource requestBundle = Samples.GetDefaultBatch().ToPoco<Bundle>();

            FhirResponse<Bundle> fhirResponse = await Client.PostBundleAsync(requestBundle);
            Assert.NotNull(fhirResponse);
            Assert.Equal(HttpStatusCode.OK, fhirResponse.StatusCode);
            foreach (var entry in fhirResponse.Resource.Entry)
            {
                if (entry.Response.Status.StartsWith("4"))
                {
                    var operationOutcome = entry.Response.Outcome as OperationOutcome;
                    Assert.NotNull(operationOutcome);
                    Assert.Equal(Hl7.Fhir.Model.ResourceType.OperationOutcome, operationOutcome.ResourceType);
                    Assert.Single(operationOutcome.Issue);
                }
            }
        }

        [Fact]
        [Trait(Traits.Priority, Priority.One)]
        public async Task WhenSubmittingABatch_GivenAProperBundleWithReadonlyUser_ThenForbiddenAndOutcomeIsReturned()
        {
            FhirClient tempClient = Client.CreateClientForUser(TestUsers.ReadOnlyUser, TestApplications.NativeClient);
            Resource requestBundle = Samples.GetDefaultBatch().ToPoco<Bundle>();

            FhirResponse<Bundle> fhirResponse = await tempClient.PostBundleAsync(requestBundle);
            Assert.NotNull(fhirResponse);
            Assert.Equal(HttpStatusCode.OK, fhirResponse.StatusCode);

            // Since first five requests are POST/PUT
            for (int i = 0; i < 5; i++)
            {
                var entry = fhirResponse.Resource.Entry[i];
                Assert.Equal(statusCodeMap[HttpStatusCode.Forbidden], entry.Response.Status);
                var operationOutcome = entry.Response.Outcome as OperationOutcome;
                Assert.NotNull(operationOutcome);
                Assert.Single(operationOutcome.Issue);

                var issue = operationOutcome.Issue.First();

                Assert.Equal(IssueSeverity.Error, issue.Severity.Value);
                Assert.Equal(IssueType.Forbidden, issue.Code);
                Assert.Equal("Authorization failed.", issue.Diagnostics);
            }
        }

        [Fact]
        [Trait(Traits.Priority, Priority.One)]
        public async Task WhenSubmittingABatch_GivenANonBundleResource_ThenBadRequestIsReturned()
        {
            FhirException ex = await Assert.ThrowsAsync<FhirException>(() => Client.PostBundleAsync(Samples.GetDefaultObservation().ToPoco<Observation>()));

            Assert.Equal(HttpStatusCode.BadRequest, ex.StatusCode);
        }

        private void ValidateResourceStatusCode(Bundle resource)
        {
            // POST: Patient
            Assert.Equal(statusCodeMap[HttpStatusCode.Created], resource.Entry[0].Response.Status);
            Assert.Equal(statusCodeMap[HttpStatusCode.OK], resource.Entry[1].Response.Status);
            Assert.Equal(statusCodeMap[HttpStatusCode.OK], resource.Entry[2].Response.Status);
            Assert.Equal(statusCodeMap[HttpStatusCode.OK], resource.Entry[3].Response.Status);
            Assert.Equal(statusCodeMap[HttpStatusCode.PreconditionFailed], resource.Entry[4].Response.Status);
            Assert.Equal(statusCodeMap[HttpStatusCode.NoContent], resource.Entry[5].Response.Status);
            Assert.Equal(statusCodeMap[HttpStatusCode.NotFound], resource.Entry[6].Response.Status);
            Assert.Equal(statusCodeMap[HttpStatusCode.NotFound], resource.Entry[7].Response.Status);
            Assert.Equal(statusCodeMap[HttpStatusCode.OK], resource.Entry[8].Response.Status);
            Assert.Equal(statusCodeMap[HttpStatusCode.NotFound], resource.Entry[9].Response.Status);
        }
    }
}
