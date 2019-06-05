﻿// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using Hl7.Fhir.ElementModel;
using Hl7.Fhir.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Internal;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Primitives;
using Microsoft.Health.Fhir.Api.Features.ContentTypes;
using Microsoft.Health.Fhir.Api.Features.Filters;
using Microsoft.Health.Fhir.Core.Exceptions;
using Microsoft.Health.Fhir.Core.Features;
using Microsoft.Health.Fhir.Core.Features.Conformance;
using Microsoft.Health.Fhir.Core.Models;
using NSubstitute;
using Xunit;
using Task = System.Threading.Tasks.Task;

namespace Microsoft.Health.Fhir.Api.UnitTests.Features.Filters
{
    public class ValidateContentTypeFilterAttributeTests
    {
        private readonly IConformanceProvider _conformanceProvider;
        private readonly CapabilityStatement _statement;

        public ValidateContentTypeFilterAttributeTests()
        {
            _statement = new CapabilityStatement
            {
                Format = new List<string> { "json" },
            };

            _conformanceProvider = Substitute.For<IConformanceProvider>();
            _conformanceProvider.GetCapabilityStatementAsync().Returns(new ResourceElement(_statement.ToTypedElement()));
        }

        [Theory]
        [InlineData("application/fhir+xml")]
        [InlineData("application/xml")]
        [InlineData("text/xml")]
        [InlineData("ttl")]
        [InlineData("xml")]
        public async Task GivenARequestWithAnInvalidFormatQuerystring_WhenValidatingTheContentType_ThenAnUnsupportedMediaTypeExceptionShouldBeThrown(string requestFormat)
        {
            var filter = CreateFilter();

            var context = CreateContext(Guid.NewGuid().ToString());
            var actionExecutedDelegate = CreateActionExecutedDelegate(context);

            context.HttpContext.Request.QueryString = new QueryString($"?_format={requestFormat}");

            await Assert.ThrowsAsync<UnsupportedMediaTypeException>(async () => await filter.OnActionExecutionAsync(context, actionExecutedDelegate));
        }

        [Theory]
        [InlineData("application/fhir+json")]
        [InlineData("application/json")]
        [InlineData("json")]
        public async Task GivenARequestWithAValidFormatQuerystring_WhenValidatingTheContentType_ThenNoExceptionShouldBeThrown(string requestFormat)
        {
            var filter = CreateFilter();

            var context = CreateContext(Guid.NewGuid().ToString());
            var actionExecutedDelegate = CreateActionExecutedDelegate(context);

            context.HttpContext.Request.QueryString = new QueryString($"?_format={requestFormat}");
            context.HttpContext.Request.Headers.Add("Accept", "application/json");

            await filter.OnActionExecutionAsync(context, actionExecutedDelegate);
        }

        [Fact]
        public async Task GivenARequestWithAValidFormatQueryStringAndNoAcceptHeader_WhenValidatingTheContentType_ThenNoExceptionShouldBeThrown()
        {
            var filter = CreateFilter();

            var context = CreateContext(Guid.NewGuid().ToString());
            var actionExecutedDelegate = CreateActionExecutedDelegate(context);

            context.HttpContext.Request.QueryString = new QueryString($"?_format=json");

            await filter.OnActionExecutionAsync(context, actionExecutedDelegate);
        }

        [Fact]
        public async Task GivenARequestWithAValidFormatQueryStringAndEmptyAcceptHeader_WhenValidatingTheContentType_ThenNoExceptionShouldBeThrown()
        {
            var filter = CreateFilter();

            var context = CreateContext(Guid.NewGuid().ToString());
            var actionExecutedDelegate = CreateActionExecutedDelegate(context);

            context.HttpContext.Request.QueryString = new QueryString($"?_format=json");
            context.HttpContext.Request.Headers.Add("Accept", string.Empty);

            await filter.OnActionExecutionAsync(context, actionExecutedDelegate);
        }

        [Theory]
        [InlineData("application/blah")]
        [InlineData("application/xml")]
        [InlineData("application/fhir+xml")]
        public async Task GivenARequestWithNotAllowedAcceptHeader_WhenValidatingTheContentType_ThenAnUnsupportedMediaTypeExceptionShouldBeThrown(string acceptHeader)
        {
            var filter = CreateFilter();

            var context = CreateContext(Guid.NewGuid().ToString());
            var actionExecutedDelegate = CreateActionExecutedDelegate(context);

            context.HttpContext.Request.Headers.Add("Accept", acceptHeader);

            await Assert.ThrowsAsync<UnsupportedMediaTypeException>(async () => await filter.OnActionExecutionAsync(context, actionExecutedDelegate));
        }

        [Theory]
        [InlineData("application/json")]
        [InlineData("application/fhir+json")]
        [InlineData("application/json+fhir")]
        [InlineData("*/*")]
        [InlineData("application/xml,application/xhtml+xml,text/html;q=0.9, text/plain;q=0.8,image/png,*/*;q=0.5")]
        public async Task GivenARequestWithAllowedAcceptHeader_WhenValidatingTheContentType_ThenNoExceptionShouldBeThrown(string acceptHeader)
        {
            var filter = CreateFilter();

            var context = CreateContext(Guid.NewGuid().ToString());
            var actionExecutedDelegate = CreateActionExecutedDelegate(context);

            context.HttpContext.Request.Headers.Add("Accept", acceptHeader);

            await filter.OnActionExecutionAsync(context, actionExecutedDelegate);
        }

        [Theory]
        [InlineData("application/blah")]
        [InlineData("application/xml")]
        [InlineData("application/fhir+xml")]
        [InlineData("")]
        public async Task GivenARequestWithNotAllowedContentTypeHeader_WhenValidatingTheContentType_ThenAnUnsupportedMediaTypeExceptionShouldBeThrown(string contentTypeHeader)
        {
            var filter = CreateFilter();

            var context = CreateContext(Guid.NewGuid().ToString());
            var actionExecutedDelegate = CreateActionExecutedDelegate(context);

            context.HttpContext.Request.Method = HttpMethod.Post.ToString();
            context.HttpContext.Request.Headers.Add("Content-Type", contentTypeHeader);

            await Assert.ThrowsAsync<UnsupportedMediaTypeException>(async () => await filter.OnActionExecutionAsync(context, actionExecutedDelegate));
        }

        [Fact]
        public async Task GivenARequestWithNoContentTypeHeader_WhenValidatingTheContentType_ThenAnUnsupportedMediaTypeExceptionShouldBeThrown()
        {
            var filter = CreateFilter();

            var context = CreateContext(Guid.NewGuid().ToString());
            var actionExecutedDelegate = CreateActionExecutedDelegate(context);

            context.HttpContext.Request.Method = HttpMethod.Post.ToString();

            await Assert.ThrowsAsync<UnsupportedMediaTypeException>(async () => await filter.OnActionExecutionAsync(context, actionExecutedDelegate));
        }

        [Theory]
        [InlineData("application/json")]
        [InlineData("application/fhir+json")]
        [InlineData("application/json+fhir")]
        public async Task GivenARequestWithAllowedContentTypeHeader_WhenValidatingTheContentType_ThenNoExceptionShouldBeThrown(string contentTypeHeader)
        {
            var filter = CreateFilter();

            var context = CreateContext(Guid.NewGuid().ToString());
            var actionExecutedDelegate = CreateActionExecutedDelegate(context);

            context.HttpContext.Request.Method = HttpMethod.Post.ToString();
            context.HttpContext.Request.Headers.Add("Content-Type", contentTypeHeader);
            context.HttpContext.Request.Headers.Add("Accept", contentTypeHeader);

            await filter.OnActionExecutionAsync(context, actionExecutedDelegate);
        }

        [Theory]
        [InlineData("json", "application/fhir+xml")]
        public async Task GivenARequestWithAnAllowedFormatAndNotAllowedContentTypeHeader_WhenValidatingTheContentType_ThenAnUnsupportedMediaTypeExceptionShouldBeThrown(string requestFormat, string contentTypeHeader)
        {
            var filter = CreateFilter();

            var context = CreateContext(Guid.NewGuid().ToString());
            var actionExecutedDelegate = CreateActionExecutedDelegate(context);

            context.HttpContext.Request.QueryString = new QueryString($"?_format={requestFormat}");
            context.HttpContext.Request.Method = HttpMethod.Post.ToString();
            context.HttpContext.Request.Headers.Add("Content-Type", contentTypeHeader);
            context.HttpContext.Request.Headers.Add("Accept", contentTypeHeader);

            await Assert.ThrowsAsync<UnsupportedMediaTypeException>(async () => await filter.OnActionExecutionAsync(context, actionExecutedDelegate));
        }

        [Theory]
        [InlineData(new[] { "json", "application/fhir+json" }, "json")]
        [InlineData(new[] { "xml", "application/fhir+xml" }, "xml")]
        public async Task GivenACapabilityStatementWithMultipleFormats_WhenValidatingTheContentType_ThenNoExceptionShouldBeThrown(string[] formats, string formatQuerystring)
        {
            var filter = CreateFilter();

            var tempFormat = _statement.Format;
            _statement.Format = formats;

            var context = CreateContext(Guid.NewGuid().ToString());
            var actionExecutedDelegate = CreateActionExecutedDelegate(context);

            context.HttpContext.Request.QueryString = new QueryString($"?_format={formatQuerystring}");
            context.HttpContext.Request.Headers.Add("Accept", formats[1]);

            await filter.OnActionExecutionAsync(context, actionExecutedDelegate);

            _statement.Format = tempFormat;
        }

        [Fact]
        public async Task GivenARequestWithAllowedAcceptHeaderAndFormatOverride_WhenSettingTheContentType_ThenClientAcceptHeadersShouldBeConsidered()
        {
            string applicationXml = "application/xml";

            _statement.Format = new[] { "xml" };

            var xmlOutput = Substitute.ForPartsOf<TextOutputFormatter>();
            xmlOutput.SupportedEncodings.Add(Encoding.UTF8);
            xmlOutput.SupportedMediaTypes.Add("application/fhir+xml");
            xmlOutput.SupportedMediaTypes.Add(applicationXml);
            xmlOutput.CanWriteResult(Arg.Any<OutputFormatterCanWriteContext>()).Returns(true);

            var filter = CreateFilter(new[] { xmlOutput });
            var acceptHeader = "application/xml,application/xhtml+xml,text/html;q=0.9, text/plain;q=0.8,image/png,*/*;q=0.5";

            var context = CreateContext(Guid.NewGuid().ToString());
            var actionExecutedDelegate = CreateActionExecutedDelegate(context);

            context.HttpContext.Request.Headers.Add("Accept", acceptHeader);
            context.HttpContext.Request.Query = new QueryCollection(new Dictionary<string, StringValues>
            {
                { KnownQueryParameterNames.Format, "xml" },
            });

            await filter.OnActionExecutionAsync(context, actionExecutedDelegate);

            Assert.Equal(applicationXml, context.HttpContext.Response.ContentType);
        }

        private ValidateContentTypeFilterAttribute CreateFilter(IEnumerable<TextOutputFormatter> formatters = null)
        {
            if (formatters == null)
            {
                var formatter = Substitute.For<TextOutputFormatter>();
                formatter.SupportedMediaTypes.Add("application/fhir+xml");
                formatter.SupportedMediaTypes.Add("application/fhir+json");
                formatter.CanWriteResult(Arg.Any<OutputFormatterCanWriteContext>()).Returns(true);

                formatters = new[] { formatter };
            }

            var service = new ContentTypeService(_conformanceProvider, formatters);

            return new ValidateContentTypeFilterAttribute(service);
        }

        private static ActionExecutingContext CreateContext(string id)
        {
            return new ActionExecutingContext(
                new ActionContext(new DefaultHttpContext(), new RouteData { Values = { ["type"] = "Observation", ["id"] = id } }, new ActionDescriptor()),
                new List<IFilterMetadata>(),
                new Dictionary<string, object>(),
                FilterTestsHelper.CreateMockFhirController());
        }

        private static ActionExecutionDelegate CreateActionExecutedDelegate(ActionExecutingContext context)
        {
            var actionExecutedContext = new ActionExecutedContext(context, context.Filters, context.Controller)
            {
                Result = context.Result,
            };
            return () => Task.FromResult(actionExecutedContext);
        }
    }
}
