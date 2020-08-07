﻿// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System;
using System.Net.Http;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using Microsoft.Health.Core;
using Microsoft.Health.Fhir.Core.Extensions;
using Microsoft.Health.Fhir.Core.Features;
using Microsoft.Health.Fhir.Core.Features.Persistence;
using Microsoft.Health.Fhir.Core.Models;
using Microsoft.Health.Fhir.Tests.Common;
using Xunit;

namespace Microsoft.Health.Fhir.Core.UnitTests.Persistence
{
    public class ResourceDeserializerTests
    {
        private readonly RawResourceFactory _rawResourceFactory;

        public ResourceDeserializerTests()
        {
            _rawResourceFactory = new RawResourceFactory(new FhirJsonSerializer());
        }

        [Fact]
        public void GivenARawResourceOfUnknownType_WhenDeserializing_ThenANotSupportedExceptionIsThrown()
        {
            var raw = new RawResource("{}", FhirResourceFormat.Unknown, versionSet: false, lastUpdatedSet: false);
            var wrapper = new ResourceWrapper("id1", "version1", "Observation", raw, new ResourceRequest(HttpMethod.Post, "http://fhir"), Clock.UtcNow, false, null, null, null);

            Assert.Throws<NotSupportedException>(() => Deserializers.ResourceDeserializer.Deserialize(wrapper));
        }

        [Fact]
        public void GivenARawResource_WhenDeserializingFromJson_ThenTheObjectIsReturned()
        {
            var observation = Samples.GetDefaultObservation()
                .UpdateId("id1");

            var wrapper = new ResourceWrapper(observation, _rawResourceFactory.Create(observation, keepMeta: true), new ResourceRequest(HttpMethod.Post, "http://fhir"), false, null, null, null);

            var newObject = Deserializers.ResourceDeserializer.Deserialize(wrapper);

            Assert.Equal(observation.Id, newObject.Id);
            Assert.Equal(observation.VersionId, newObject.VersionId);
        }

        [Fact]
        public void GivenAResourceWrapper_WhenDeserializingToJsonDocumentAndVersionIdNotSet_UpdatedWithVersionIdFromResourceWrapper()
        {
            var patient = Samples.GetDefaultPatient().UpdateVersion("3").UpdateLastUpdated(Clock.UtcNow - TimeSpan.FromDays(30));

            var wrapper = new ResourceWrapper(patient, _rawResourceFactory.Create(patient, keepMeta: false), new ResourceRequest(HttpMethod.Post, "http://fhir"), false, null, null, null);
            wrapper.Version = "2";

            var (rawString, _) = ResourceDeserializer.DeserializeToJsonDocument(wrapper);
            Assert.NotNull(rawString);

            var deserialized = new FhirJsonParser(DefaultParserSettings.Settings).Parse<Patient>(rawString);

            Assert.Equal(wrapper.Version, deserialized.VersionId);
            Assert.Equal(wrapper.LastModified, deserialized.Meta.LastUpdated);
        }

        [Fact]
        public void GivenAResourceWrapper_WhenDeserializingToJsonDocumentAndVersionIdSet_MaintainsVersionIdInRawResourceString()
        {
            var lastUpdated = Clock.UtcNow - TimeSpan.FromDays(30);
            var patient = Samples.GetDefaultPatient().UpdateVersion("3").UpdateLastUpdated(lastUpdated);

            var wrapper = new ResourceWrapper(patient, _rawResourceFactory.Create(patient, keepMeta: true), new ResourceRequest(HttpMethod.Post, "http://fhir"), false, null, null, null);
            wrapper.Version = "2";

            var (rawString, _) = ResourceDeserializer.DeserializeToJsonDocument(wrapper);
            Assert.NotNull(rawString);

            var deserialized = new FhirJsonParser(DefaultParserSettings.Settings).Parse<Patient>(rawString);

            Assert.Equal("3", deserialized.VersionId);
            Assert.Equal(lastUpdated, deserialized.Meta.LastUpdated);
        }
    }
}
