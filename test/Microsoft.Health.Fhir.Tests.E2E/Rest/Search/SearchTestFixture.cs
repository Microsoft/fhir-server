﻿// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using Microsoft.Health.Fhir.Tests.Common;
using Microsoft.Health.Fhir.Web;
using FhirClient = Microsoft.Health.Fhir.Tests.E2E.Common.FhirClient;

namespace Microsoft.Health.Fhir.Tests.E2E.Rest.Search
{
    public class SearchTestFixture : HttpIntegrationTestFixture<Startup>
    {
        private SearchFhirClient _fhirClient;

        public string TestSessionId
        {
            get => _fhirClient.TestSessionId;
            set => _fhirClient.TestSessionId = value;
        }

        public override FhirClient FhirClient
            => _fhirClient ?? (_fhirClient = new SearchFhirClient(HttpClient, ResourceFormat.Json, Guid.NewGuid().ToString()));

        public async Task<TResource[]> CreateResourcesAsync<TResource>(Func<TResource, List<Identifier>> identifiersGetter, params Action<TResource>[] resourceCustomizer)
            where TResource : Resource, new()
        {
            var resources = new TResource[resourceCustomizer.Length];

            for (int i = 0; i < resources.Length; i++)
            {
                var resource = new TResource();

                resourceCustomizer[i](resource);

                resources[i] = await CreateResourceAsync(identifiersGetter, resource);
            }

            return resources;
        }

        public async Task<TResource[]> CreateResourcesAsync<TResource>(Func<TResource, List<Identifier>> identifiersGetter, int count)
           where TResource : Resource, new()
        {
            var resources = new TResource[count];

            for (int i = 0; i < resources.Length; i++)
            {
                var resource = new TResource();

                resources[i] = await CreateResourceAsync(identifiersGetter, resource);
            }

            return resources;
        }

        public async Task<TResource> CreateResourceAsync<TResource>(Func<TResource, List<Identifier>> identifiersGetter, string sampleResourceName)
           where TResource : Resource, new()
        {
            TResource resource = Samples.GetJsonSample<TResource>(sampleResourceName);

            return await CreateResourceAsync(identifiersGetter, resource);
        }

        private async Task<TResource> CreateResourceAsync<TResource>(Func<TResource, List<Identifier>> identifiersGetter, TResource resource)
           where TResource : Resource, new()
        {
            identifiersGetter(resource).Add(new Identifier(null, TestSessionId));

            return await FhirClient.CreateAsync(resource);
        }
    }
}
