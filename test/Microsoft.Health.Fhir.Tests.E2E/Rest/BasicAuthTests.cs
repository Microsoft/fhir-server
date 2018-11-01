﻿// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System;
using System.Net;
using System.Threading.Tasks;
using Hl7.Fhir.Model;
using Microsoft.Health.Fhir.Tests.Common;
using Microsoft.Health.Fhir.Tests.E2E.Common;
using Microsoft.Health.Fhir.Web;
using Xunit;
using FhirClient = Microsoft.Health.Fhir.Tests.E2E.Common.FhirClient;
using Task = System.Threading.Tasks.Task;

namespace Microsoft.Health.Fhir.Tests.E2E.Rest
{
    /// <summary>
    /// NOTE: These tests will fail if security is disabled.
    /// </summary>
    [Trait(Traits.Category, Categories.Authorization)]
    public class BasicAuthTests : IClassFixture<HttpIntegrationTestFixture<Startup>>
    {
        private const string ForbiddenMessage = "Forbidden: Authorization failed.";

        public BasicAuthTests(HttpIntegrationTestFixture<Startup> fixture)
        {
            Client = fixture.FhirClient;
        }

        protected FhirClient Client { get; set; }

        [Fact]
        [Trait(Traits.Priority, Priority.One)]
        public async Task WhenGettingAResource_GivenAUserWithNoReadPermissions_TheServerShouldReturnForbidden()
        {
            await Client.RunAsClientApplication(TestApplications.ServiceClient);
            Observation createdResource = await Client.CreateAsync(Samples.GetDefaultObservation());

            await Client.RunAsUser(TestUsers.WriteOnlyUser, TestApplications.NativeClient);
            FhirException fhirException = await Assert.ThrowsAsync<FhirException>(async () => await Client.ReadAsync<Observation>(ResourceType.Observation, createdResource.Id));
            Assert.Equal(ForbiddenMessage, fhirException.Message);
            Assert.Equal(HttpStatusCode.Forbidden, fhirException.StatusCode);
        }

        [Fact]
        [Trait(Traits.Priority, Priority.One)]
        public async Task WhenCreatingAResource_GivenAUserWithNoCreatePermissions_TheServerShouldReturnForbidden()
        {
            await Client.RunAsUser(TestUsers.ReadOnlyUser, TestApplications.NativeClient);
            FhirException fhirException = await Assert.ThrowsAsync<FhirException>(async () => await Client.CreateAsync(Samples.GetDefaultObservation()));
            Assert.Equal(ForbiddenMessage, fhirException.Message);
            Assert.Equal(HttpStatusCode.Forbidden, fhirException.StatusCode);
        }

        [Fact]
        [Trait(Traits.Priority, Priority.One)]
        public async Task WhenUpdatingAResource_GivenAUserWithNoWritePermissions_TheServerShouldReturnForbidden()
        {
            await Client.RunAsUser(TestUsers.WriteOnlyUser, TestApplications.NativeClient);
            Observation createdResource = await Client.CreateAsync(Samples.GetDefaultObservation());

            await Client.RunAsUser(TestUsers.ReadOnlyUser, TestApplications.NativeClient);
            FhirException fhirException = await Assert.ThrowsAsync<FhirException>(async () => await Client.UpdateAsync(createdResource));
            Assert.Equal(ForbiddenMessage, fhirException.Message);
            Assert.Equal(HttpStatusCode.Forbidden, fhirException.StatusCode);
        }

        [Fact]
        [Trait(Traits.Priority, Priority.One)]
        public async Task WhenHardDeletingAResource_GivenAUserWithNoHardDeletePermissions_TheServerShouldReturnForbidden()
        {
            await Client.RunAsUser(TestUsers.WriteOnlyUser, TestApplications.NativeClient);
            Observation createdResource = await Client.CreateAsync(Samples.GetDefaultObservation());

            FhirException fhirException = await Assert.ThrowsAsync<FhirException>(async () => await Client.HardDeleteAsync(createdResource));
            Assert.Equal(ForbiddenMessage, fhirException.Message);
            Assert.Equal(HttpStatusCode.Forbidden, fhirException.StatusCode);
        }

        [Fact]
        [Trait(Traits.Priority, Priority.One)]
        public async Task WhenHardDeletingAResource_GivenAUserWithHardDeletePermissions_TheServerShouldReturnSuccess()
        {
            await Client.RunAsUser(TestUsers.WriteOnlyUser, TestApplications.NativeClient);
            Observation createdResource = await Client.CreateAsync(Samples.GetDefaultObservation());

            await Client.RunAsUser(TestUsers.HardDeleteUser, TestApplications.NativeClient);

            // Hard-delete the resource.
            await Client.HardDeleteAsync(createdResource);

            await Client.RunAsUser(TestUsers.ReadOnlyUser, TestApplications.NativeClient);

            // Getting the resource should result in NotFound.
            await ExecuteAndValidateNotFoundStatus(() => Client.ReadAsync<Observation>(ResourceType.Observation, createdResource.Id));

            async Task<FhirException> ExecuteAndValidateNotFoundStatus(Func<Task> action)
            {
                FhirException exception = await Assert.ThrowsAsync<FhirException>(action);
                Assert.Equal(HttpStatusCode.NotFound, exception.StatusCode);
                return exception;
            }
        }

        [Fact]
        [Trait(Traits.Priority, Priority.One)]
        public async Task WhenGettingAResource_GivenAUserWithReadPermissions_TheServerShouldReturnSuccess()
        {
            await Client.RunAsClientApplication(TestApplications.ServiceClient);
            Observation createdResource = await Client.CreateAsync(Samples.GetDefaultObservation());

            await Client.RunAsUser(TestUsers.ReadOnlyUser, TestApplications.NativeClient);
            FhirResponse<Observation> readResponse = await Client.ReadAsync<Observation>(ResourceType.Observation, createdResource.Id);

            Observation readResource = readResponse.Resource;

            Assert.Equal(createdResource.Id, readResource.Id);
            Assert.Equal(createdResource.Meta.VersionId, readResource.Meta.VersionId);
            Assert.Equal(createdResource.Meta.LastUpdated, readResource.Meta.LastUpdated);
        }

        [Fact]
        [Trait(Traits.Priority, Priority.One)]
        public async Task WhenUpdatingAResource_GivenAUserWithUpdatePermissions_TheServerShouldReturnSuccess()
        {
            await Client.RunAsUser(TestUsers.AdminUser, TestApplications.NativeClient);
            Observation createdResource = await Client.CreateAsync(Samples.GetDefaultObservation());

            await Client.RunAsUser(TestUsers.ReadWriteUser, TestApplications.NativeClient);
            FhirResponse<Observation> updateResponse = await Client.UpdateAsync(createdResource);

            Assert.Equal(System.Net.HttpStatusCode.OK, updateResponse.StatusCode);

            Observation updatedResource = updateResponse.Resource;

            Assert.NotNull(updatedResource);
            Assert.Equal(createdResource.Id, updatedResource.Id);
            Assert.NotEqual(createdResource.Meta.VersionId, updatedResource.Meta.VersionId);
            Assert.NotEqual(createdResource.Meta.LastUpdated, updatedResource.Meta.LastUpdated);
        }
    }
}
