﻿// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EnsureThat;
using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Health.Fhir.Api.Features.Formatters;
using Microsoft.Health.Fhir.Core.Exceptions;
using Microsoft.Health.Fhir.Core.Features;
using Microsoft.Health.Fhir.Core.Features.Conformance;
using Microsoft.Net.Http.Headers;
using Task = System.Threading.Tasks.Task;

namespace Microsoft.Health.Fhir.Api.Features.ContentTypes
{
    internal class ContentTypeService : IContentTypeService
    {
        private readonly IConformanceProvider _conformanceProvider;
        private readonly ICollection<TextOutputFormatter> _outputFormatters;

        public ContentTypeService(
            IConformanceProvider conformanceProvider,
            IEnumerable<TextOutputFormatter> outputFormatters)
        {
            EnsureArg.IsNotNull(conformanceProvider, nameof(conformanceProvider));
            EnsureArg.IsNotNull(outputFormatters, nameof(outputFormatters));

            _conformanceProvider = conformanceProvider;
            _outputFormatters = outputFormatters.ToArray();
        }

        public async Task CheckRequestedContentTypeAsync(HttpContext contextHttpContext)
        {
            var acceptHeaders = contextHttpContext.Request.GetTypedHeaders().Accept;
            var format = GetSpecifiedFormat(contextHttpContext);

            // Check the _format first since it takes precedence over the accept header.
            if (!string.IsNullOrEmpty(format))
            {
                ResourceFormat resourceFormat = ContentType.GetResourceFormatFromFormatParam(format);
                if (!await IsFormatSupportedAsync(resourceFormat))
                {
                    throw new UnsupportedMediaTypeException(Resources.UnsupportedFormatParameter);
                }

                string closestClientMediaType = _outputFormatters.GetClosestClientMediaType(resourceFormat, acceptHeaders.Select(x => x.MediaType.Value));

                // Overrides output format type
                contextHttpContext.Response.ContentType = closestClientMediaType;
            }
            else
            {
                if (acceptHeaders != null && acceptHeaders.All(a => a.MediaType != "*/*"))
                {
                    var isAcceptHeaderValid = false;

                    foreach (MediaTypeHeaderValue acceptHeader in acceptHeaders)
                    {
                        ResourceFormat resourceFormat = ContentType.GetResourceFormatFromContentType(acceptHeader.MediaType.ToString());

                        isAcceptHeaderValid = await IsFormatSupportedAsync(resourceFormat);

                        if (isAcceptHeaderValid)
                        {
                            break;
                        }
                    }

                    if (!isAcceptHeaderValid)
                    {
                        throw new UnsupportedMediaTypeException(Resources.UnsupportedAcceptHeader);
                    }
                }
            }
        }

        private static string GetSpecifiedFormat(HttpContext context)
        {
            EnsureArg.IsNotNull(context, nameof(context));

            // If executing in a rethrown error context, ensure we carry the specified format
            var previous = context.Features.Get<IStatusCodeReExecuteFeature>()?.OriginalQueryString;
            var previousQuery = QueryHelpers.ParseNullableQuery(previous);

            if (previousQuery?.TryGetValue(KnownQueryParameterNames.Format, out var originFormatValues) == true)
            {
                return originFormatValues.FirstOrDefault();
            }

            // Check the current query string
            if (context.Request.Query.TryGetValue(KnownQueryParameterNames.Format, out var queryValues))
            {
                return queryValues.FirstOrDefault();
            }

            return null;
        }

        public async Task<bool> IsFormatSupportedAsync(ResourceFormat resourceFormat)
        {
            CapabilityStatement statement = await _conformanceProvider.GetCapabilityStatementAsync();

            switch (resourceFormat)
            {
                case ResourceFormat.Json:
                    return statement.Format.Any(f => f.Contains("json", StringComparison.Ordinal));

                case ResourceFormat.Xml:
                    return statement.Format.Any(f => f.Contains("xml", StringComparison.Ordinal));

                default:
                    return false;
            }
        }
    }
}
