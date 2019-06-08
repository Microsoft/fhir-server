﻿// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System.Collections.Generic;
using Hl7.Fhir.Model;
using Microsoft.Health.Fhir.Tests.Common.FixtureParameters;
using Microsoft.Health.Fhir.Tests.E2E.Common;
using Microsoft.Health.Fhir.Web;

namespace Microsoft.Health.Fhir.Tests.E2E.Rest.Search
{
    public class UriSearchTestFixture : HttpIntegrationTestFixture<Startup>
    {
        public UriSearchTestFixture(DataStore dataStore, Format format)
            : base(dataStore, format)
        {
            // Prepare the resources used for URI search tests.
            FhirClient.DeleteAllResources(ResourceType.ValueSet).Wait();

            ValueSets = FhirClient.CreateResourcesAsync<ValueSet>(
                vs => AddValueSet(vs, "http://somewhere.com/test/system"),
                vs => AddValueSet(vs, "urn://localhost/test")).Result;

            void AddValueSet(ValueSet vs, string url)
            {
                vs.Status = PublicationStatus.Active;
                vs.Url = url;
            }
        }

        public IReadOnlyList<ValueSet> ValueSets { get; }
    }
}
