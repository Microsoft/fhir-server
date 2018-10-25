﻿// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System.Linq;
using Microsoft.Health.Fhir.Core.Configs;
using Microsoft.Health.Fhir.Core.Exceptions;
using Microsoft.Health.Fhir.Tests.Common;
using Xunit;

namespace Microsoft.Health.Fhir.Core.UnitTests.Features.Security
{
    public class AuthPermissionConfigTests
    {
        [Fact]
        public void GivenAValidAuthorizationConfiguration_WhenDeserailized_ThenReturnsExpectedRoleInformation()
        {
            var authorizationConfiguration = Samples.GetJsonSample<AuthorizationConfiguration>("AuthConfigWithValidRoles");
            authorizationConfiguration.ValidateRoles();

            Assert.NotNull(authorizationConfiguration);
            Assert.NotNull(authorizationConfiguration.Roles);
            Assert.Equal(3, authorizationConfiguration.Roles.Count());
        }

        [Fact]
        public void GivenAnInvalidAuthorizationConfigurationForRoleWithNoActions_WhenValidated_ThrowAppropriateValidationException()
        {
            var invalidAuthorizationConfiguration = Samples.GetJsonSample<AuthorizationConfiguration>("AuthConfigWithInvalidEntries");
            InvalidDefinitionException validationException = Assert.Throws<InvalidDefinitionException>(() => invalidAuthorizationConfiguration.ValidateRoles());

            Assert.NotNull(validationException.Issues.SingleOrDefault(issueComp => issueComp.Diagnostics.Equals("ResourcePermission for Role 'Nurse' does not have any Actions.")));
        }
    }
}
