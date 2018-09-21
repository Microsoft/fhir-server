// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Health.Fhir.Api.Configs;
using Microsoft.Health.Fhir.Api.Controllers;
using Microsoft.Health.Fhir.Core.Features.Context;
using Microsoft.Health.Fhir.Core.Features.Routing;
using NSubstitute;
using Xunit;

namespace Microsoft.Health.Fhir.Api.UnitTests.Controllers
{
    public class FhirControllerTests
    {
        private readonly IMediator _mediator = Substitute.For<IMediator>();
        private readonly ILogger<FhirController> _logger = NullLogger<FhirController>.Instance;
        private readonly IFhirRequestContextAccessor _contextAccessor = Substitute.For<IFhirRequestContextAccessor>();
        private readonly IUrlResolver _urlResolver = Substitute.For<IUrlResolver>();
        private readonly FeatureConfiguration _featureConfiguration = new FeatureConfiguration();

        private readonly FhirController _controller;

        public FhirControllerTests()
        {
            _controller = new FhirController(
                _mediator,
                _logger,
                _contextAccessor,
                _urlResolver,
                Options.Create(_featureConfiguration));
        }

        [Fact]
        public void GivenUIIsSupported_WhenRequestingForRoot_ThenViewResultShouldBeReturned()
        {
            _featureConfiguration.SupportsUI = true;

            IActionResult result = _controller.Fhir();

            Assert.IsType<ViewResult>(result);
        }

        [Fact]
        public void GivenUIIsNotSupported_WhenRequestingForRoot_ThenNotFoundResultShouldBeReturned()
        {
            _featureConfiguration.SupportsUI = false;

            IActionResult result = _controller.Fhir();

            Assert.IsType<NotFoundResult>(result);
        }
    }
}
