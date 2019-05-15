﻿// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using Hl7.Fhir.Model;
using Microsoft.Health.Fhir.Core.Extensions;
using Microsoft.Health.Fhir.Core.Features.Definition;
using Microsoft.Health.Fhir.Core.Features.Persistence;
using Microsoft.Health.Fhir.Core.Features.Search;
using Microsoft.Health.Fhir.Core.Features.Search.SearchValues;
using Microsoft.Health.Fhir.Core.Models;
using NSubstitute;
using Xunit;

namespace Microsoft.Health.Fhir.Core.UnitTests.Features.Compartment
{
    public class CompartmentIndexerTests
    {
        public CompartmentIndexerTests()
        {
            ModelExtensions.SetModelInfoProvider();
        }

        [Theory]
        [InlineData(ResourceType.Observation, CompartmentType.Patient, "123")]
        [InlineData(ResourceType.Account, CompartmentType.Encounter, "example")]
        [InlineData(ResourceType.Account, CompartmentType.Device, "example1")]
        [InlineData(ResourceType.Account, CompartmentType.Practitioner, "example55")]
        [InlineData(ResourceType.Account, CompartmentType.RelatedPerson, "example4")]
        public void GivenSearchIndicesWithResourceTypeAndCompartmentType_WhenExtracted_ThenCorrectIndicesExtracted(ResourceType resourceType, CompartmentType compartmentType, string resourceId)
        {
            var compartmentManager = Substitute.For<ICompartmentDefinitionManager>();
            var compartmentIndexer = new CompartmentIndexer(compartmentManager);

            HashSet<string> compParams = null;
            compartmentManager.TryGetSearchParams(resourceType.ToString(), compartmentType.ToString(), out compParams)
                .Returns(x =>
                {
                    x[2] = new HashSet<string> { "testParam" };
                    return true;
                });
            var searchIndexEntries = new List<SearchIndexEntry> { new SearchIndexEntry(new SearchParameterInfo("testParam"), new ReferenceSearchValue(ReferenceKind.Internal, new Uri("http://localhost"), CompartmentDefinitionManager.CompartmentTypeToResourceType(compartmentType.ToString()).ToString(), resourceId)) };
            CompartmentIndices compartmentIndices = compartmentIndexer.Extract(resourceType.ToString(), searchIndexEntries);

            IReadOnlyCollection<string> resourceIds = null;
            if (compartmentType == CompartmentType.Device)
            {
                resourceIds = compartmentIndices.DeviceCompartmentEntry;
            }
            else if (compartmentType == CompartmentType.Encounter)
            {
                resourceIds = compartmentIndices.EncounterCompartmentEntry;
            }
            else if (compartmentType == CompartmentType.Patient)
            {
                resourceIds = compartmentIndices.PatientCompartmentEntry;
            }
            else if (compartmentType == CompartmentType.Practitioner)
            {
                resourceIds = compartmentIndices.PractitionerCompartmentEntry;
            }
            else if (compartmentType == CompartmentType.RelatedPerson)
            {
                resourceIds = compartmentIndices.RelatedPersonCompartmentEntry;
            }

            Assert.Single(resourceIds);
            Assert.Contains(resourceId, resourceIds);
        }
    }
}
