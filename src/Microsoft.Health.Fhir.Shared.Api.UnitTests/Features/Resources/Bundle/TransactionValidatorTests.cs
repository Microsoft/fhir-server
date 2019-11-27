﻿// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using Microsoft.Health.Fhir.Api.Features.Resources.Bundle;
using Microsoft.Health.Fhir.Core.Exceptions;
using Microsoft.Health.Fhir.Core.Extensions;
using Microsoft.Health.Fhir.Tests.Common;
using Xunit;

namespace Microsoft.Health.Fhir.Api.UnitTests.Features.Resources.Bundle
{
    public class TransactionValidatorTests
    {
        [Fact]
        public void GivenABundleWithUniqueResources_TransactionValidatorShouldNotThrowException()
        {
            ValidateIfBundleEntryIsUnique(Samples.GetDefaultTransaction());
        }

        [Theory]
        [InlineData("Bundle-TransactionWithMultipleResourcesWithSameFullUrl")]
        [InlineData("Bundle-TransactionWithMultipleEntriesModifyingSameResource")]
        public void GivenATransactionBundle_IfContainsMultipleEntriesWithTheSameResource_TransactionValidatorShouldThrowException(string inputBundle)
        {
            var expectedMessage = "Bundle contains multiple resources with the same url value 'Patient/123'.";

            var requestBundle = Samples.GetJsonSample(inputBundle);
            var exception = Assert.Throws<RequestNotValidException>(() => ValidateIfBundleEntryIsUnique(requestBundle));
            Assert.Equal(expectedMessage, exception.Message);
        }

        private static void ValidateIfBundleEntryIsUnique(Core.Models.ResourceElement requestBundle)
        {
            TransactionValidator.ValidateTransactionBundle(requestBundle.ToPoco<Hl7.Fhir.Model.Bundle>());
        }
    }
}
