﻿// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using EnsureThat;
using Hl7.Fhir.Model;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Microsoft.Health.Fhir.Api.Configs;
using Microsoft.Health.Fhir.Api.Features.ActionResults;
using Microsoft.Health.Fhir.Api.Features.Context;
using Microsoft.Health.Fhir.Api.Features.Filters;
using Microsoft.Health.Fhir.Api.Features.Headers;
using Microsoft.Health.Fhir.Api.Features.Routing;
using Microsoft.Health.Fhir.Api.Features.Security;
using Microsoft.Health.Fhir.Core.Extensions;
using Microsoft.Health.Fhir.Core.Features;
using Microsoft.Health.Fhir.Core.Features.Context;
using Microsoft.Health.Fhir.Core.Features.Persistence;
using Microsoft.Health.Fhir.Core.Features.Routing;
using Microsoft.Health.Fhir.Core.Messages.Search;
using Microsoft.Health.Fhir.Core.Models;
using Microsoft.Health.Fhir.ValueSets;
using Microsoft.Net.Http.Headers;

namespace Microsoft.Health.Fhir.Api.Controllers
{
    /// <summary>
    /// FHIR Rest API
    /// </summary>
    [ServiceFilter(typeof(AuditLoggingFilterAttribute), Order = -1)]
    [ServiceFilter(typeof(OperationOutcomeExceptionFilterAttribute))]
    [ServiceFilter(typeof(ValidateContentTypeFilterAttribute))]
    [ValidateResourceTypeFilter]
    [ValidateModelState]
    [Authorize(PolicyNames.FhirPolicy)]
    public class FhirController : Controller
    {
        private readonly IMediator _mediator;
        private readonly ILogger<FhirController> _logger;
        private readonly IFhirRequestContextAccessor _fhirRequestContextAccessor;
        private readonly IUrlResolver _urlResolver;
        private readonly IAuthorizationService _authorizationService;
        private readonly FeatureConfiguration _featureConfiguration;

        /// <summary>
        /// Initializes a new instance of the <see cref="FhirController" /> class.
        /// </summary>
        /// <param name="mediator">The mediator.</param>
        /// <param name="logger">The logger.</param>
        /// <param name="fhirRequestContextAccessor">The FHIR request context accessor.</param>
        /// <param name="urlResolver">The urlResolver.</param>
        /// <param name="uiConfiguration">The UI configuration.</param>
        /// <param name="authorizationService">The authorization service.</param>
        public FhirController(
            IMediator mediator,
            ILogger<FhirController> logger,
            IFhirRequestContextAccessor fhirRequestContextAccessor,
            IUrlResolver urlResolver,
            IOptions<FeatureConfiguration> uiConfiguration,
            IAuthorizationService authorizationService)
        {
            EnsureArg.IsNotNull(mediator, nameof(mediator));
            EnsureArg.IsNotNull(logger, nameof(logger));
            EnsureArg.IsNotNull(fhirRequestContextAccessor, nameof(fhirRequestContextAccessor));
            EnsureArg.IsNotNull(urlResolver, nameof(urlResolver));
            EnsureArg.IsNotNull(uiConfiguration, nameof(uiConfiguration));
            EnsureArg.IsNotNull(uiConfiguration.Value, nameof(uiConfiguration));
            EnsureArg.IsNotNull(authorizationService, nameof(authorizationService));

            _mediator = mediator;
            _logger = logger;
            _fhirRequestContextAccessor = fhirRequestContextAccessor;
            _urlResolver = urlResolver;
            _authorizationService = authorizationService;
            _featureConfiguration = uiConfiguration.Value;
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        [Route("CustomError")]
        [AllowAnonymous]
        public IActionResult CustomError(int? statusCode = null)
        {
            HttpStatusCode returnCode;
            OperationOutcome.IssueType issueType;
            string diagnosticInfo;

            switch (statusCode)
            {
                case (int)HttpStatusCode.Unauthorized:
                    issueType = OperationOutcome.IssueType.Login;
                    returnCode = HttpStatusCode.Unauthorized;
                    diagnosticInfo = Resources.Unauthorized;
                    break;
                case (int)HttpStatusCode.Forbidden:
                    issueType = OperationOutcome.IssueType.Forbidden;
                    returnCode = HttpStatusCode.Forbidden;
                    diagnosticInfo = Resources.Forbidden;
                    break;
                case (int)HttpStatusCode.NotFound:
                    issueType = OperationOutcome.IssueType.NotFound;
                    returnCode = HttpStatusCode.NotFound;
                    diagnosticInfo = Resources.NotFoundException;
                    break;
                default:
                    issueType = OperationOutcome.IssueType.Exception;
                    returnCode = HttpStatusCode.InternalServerError;
                    diagnosticInfo = Resources.GeneralInternalError;
                    break;
            }

            return FhirResult.Create(
                new OperationOutcome
                {
                    Id = _fhirRequestContextAccessor.FhirRequestContext.CorrelationId,
                    Issue = new List<OperationOutcome.IssueComponent>
                    {
                        new OperationOutcome.IssueComponent
                        {
                            Severity = OperationOutcome.IssueSeverity.Error,
                            Code = issueType,
                            Diagnostics = diagnosticInfo,
                        },
                    },
                }, returnCode);
        }

        /// <summary>
        /// Creates a new resource
        /// </summary>
        /// <param name="resource">The resource.</param>
        [HttpPost]
        [Route(KnownRoutes.ResourceType)]
        [AuditEventSubType(AuditEventSubType.Create)]
        [Authorize(PolicyNames.WritePolicy)]
        public async Task<IActionResult> Create([FromBody] Resource resource)
        {
            Resource response = await _mediator.CreateResourceAsync(resource, HttpContext.RequestAborted);

            return FhirResult.Create(response, HttpStatusCode.Created)
                .SetETagHeader()
                .SetLastModifiedHeader()
                .SetLocationHeader(_urlResolver);
        }

        /// <summary>
        /// Updates or creates a new resource
        /// </summary>
        /// <param name="resource">The resource.</param>
        [HttpPut]
        [ValidateResourceIdFilter]
        [Route(KnownRoutes.ResourceTypeById)]
        [AuditEventSubType(AuditEventSubType.Update)]
        [Authorize(PolicyNames.WritePolicy)]
        public async Task<IActionResult> Update([FromBody] Resource resource)
        {
            var suppliedWeakETag = HttpContext.Request.Headers[HeaderNames.IfMatch];

            WeakETag weakETag = null;
            if (!string.IsNullOrWhiteSpace(suppliedWeakETag))
            {
                weakETag = WeakETag.FromWeakETag(suppliedWeakETag);
            }

            SaveOutcome response = await _mediator.UpsertResourceAsync(resource, weakETag, HttpContext.RequestAborted);

            switch (response.Outcome)
            {
                case SaveOutcomeType.Created:
                    return FhirResult.Create(response.Resource, HttpStatusCode.Created)
                        .SetETagHeader()
                        .SetLastModifiedHeader()
                        .SetLocationHeader(_urlResolver);
                case SaveOutcomeType.Updated:
                    return FhirResult.Create(response.Resource, HttpStatusCode.OK)
                        .SetETagHeader()
                        .SetLastModifiedHeader();
            }

            return FhirResult.Create(response.Resource, HttpStatusCode.BadRequest);
        }

        /// <summary>
        /// Reads the specified resource.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <param name="id">The identifier.</param>
        [HttpGet]
        [Route(KnownRoutes.ResourceTypeById, Name = RouteNames.ReadResource)]
        [AuditEventSubType(AuditEventSubType.Read)]
        [Authorize(PolicyNames.ReadPolicy)]
        public async Task<IActionResult> Read(string type, string id)
        {
            Resource response = await _mediator.GetResourceAsync(new ResourceKey(type, id), HttpContext.RequestAborted);

            return FhirResult.Create(response)
                .SetETagHeader()
                .SetLastModifiedHeader();
        }

        /// <summary>
        /// Returns the history of all resources in the system
        /// </summary>
        /// <param name="at">Instant for history to return.</param>
        /// <param name="since">Starting time for history to return.</param>
        /// <param name="count">Number of items to return.</param>
        /// <param name="ct">Continuation token.</param>
        [HttpGet]
        [Route(KnownRoutes.History, Name = RouteNames.History)]
        [AuditEventSubType(AuditEventSubType.HistorySystem)]
        [Authorize(PolicyNames.ReadPolicy)]
        public async Task<IActionResult> SystemHistory(
            [FromQuery(Name = KnownQueryParameterNames.At)] PartialDateTime at,
            [FromQuery(Name = KnownQueryParameterNames.Since)] PartialDateTime since,
            [FromQuery(Name = KnownQueryParameterNames.Count)] int? count,
            string ct)
        {
            Bundle response = await _mediator.SearchResourceHistoryAsync(since, at, count, ct, HttpContext.RequestAborted);

            return FhirResult.Create(response);
        }

        /// <summary>
        /// Returns the history of a specific resource type
        /// </summary>
        /// <param name="type">The resource type.</param>
        /// <param name="at">Instant for history to return.</param>
        /// <param name="since">Starting time for history to return.</param>
        /// <param name="count">Number of items to return.</param>
        /// <param name="ct">Continuation token.</param>
        [HttpGet]
        [Route(KnownRoutes.ResourceTypeHistory, Name = RouteNames.HistoryType)]
        [AuditEventSubType(AuditEventSubType.HistoryType)]
        [Authorize(PolicyNames.ReadPolicy)]
        public async Task<IActionResult> TypeHistory(
            string type,
            [FromQuery(Name = KnownQueryParameterNames.At)] PartialDateTime at,
            [FromQuery(Name = KnownQueryParameterNames.Since)] PartialDateTime since,
            [FromQuery(Name = KnownQueryParameterNames.Count)] int? count,
            string ct)
        {
            Bundle response = await _mediator.SearchResourceHistoryAsync(type, since, at, count, ct, HttpContext.RequestAborted);

            return FhirResult.Create(response);
        }

        /// <summary>
        /// Returns the history of a resource
        /// </summary>
        /// <param name="type">The resource type.</param>
        /// <param name="id">The identifier.</param>
        /// <param name="at">Instant for history to return.</param>
        /// <param name="since">Starting time for history to return.</param>
        /// <param name="count">Number of items to return.</param>
        /// <param name="ct">Continuation token.</param>
        [HttpGet]
        [Route(KnownRoutes.ResourceTypeByIdHistory, Name = RouteNames.HistoryTypeId)]
        [AuditEventSubType(AuditEventSubType.HistoryInstance)]
        [Authorize(PolicyNames.ReadPolicy)]
        public async Task<IActionResult> History(
            string type,
            string id,
            [FromQuery(Name = KnownQueryParameterNames.At)] PartialDateTime at,
            [FromQuery(Name = KnownQueryParameterNames.Since)] PartialDateTime since,
            [FromQuery(Name = KnownQueryParameterNames.Count)] int? count,
            string ct)
        {
            Bundle response = await _mediator.SearchResourceHistoryAsync(type, id, since, at, count, ct, HttpContext.RequestAborted);

            return FhirResult.Create(response);
        }

        /// <summary>
        /// Reads the specified version of the resource.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <param name="id">The identifier.</param>
        /// <param name="vid">The versionId.</param>
        [HttpGet]
        [Route(KnownRoutes.ResourceTypeByIdAndVid, Name = RouteNames.ReadResourceWithVersionRoute)]
        [AuditEventSubType(AuditEventSubType.VRead)]
        [Authorize(PolicyNames.ReadPolicy)]
        public async Task<IActionResult> VRead(string type, string id, string vid)
        {
            var response = await _mediator.GetResourceAsync(new ResourceKey(type, id, vid), HttpContext.RequestAborted);

            return FhirResult.Create(response, HttpStatusCode.OK)
                    .SetETagHeader()
                    .SetLastModifiedHeader();
        }

        /// <summary>
        /// Deletes the specified resource
        /// </summary>
        /// <param name="type">The type.</param>
        /// <param name="id">The identifier.</param>
        /// <param name="hardDelete">A flag indicating whether to hard-delete the resource or not.</param>
        [HttpDelete]
        [Route(KnownRoutes.ResourceTypeById)]
        [AuditEventSubType(AuditEventSubType.Delete)]
        public async Task<IActionResult> Delete(string type, string id, [FromQuery]bool hardDelete)
        {
            string policy = PolicyNames.WritePolicy;
            if (hardDelete)
            {
                policy = PolicyNames.HardDeletePolicy;
            }

            AuthorizationResult authorizationResult = await _authorizationService.AuthorizeAsync(User, policy);
            if (!authorizationResult.Succeeded)
            {
                return Forbid();
            }

            var response = await _mediator.DeleteResourceAsync(new ResourceKey(type, id), hardDelete, HttpContext.RequestAborted);

            return FhirResult.NoContent().SetETagHeader(response.WeakETag);
        }

        /// <summary>
        /// Searches across all resource types.
        /// </summary>
        [Route("", Name = RouteNames.SearchAllResources)]
        [AuditEventSubType(AuditEventSubType.SearchSystem)]
        [Authorize(PolicyNames.ReadPolicy)]
        public async Task<IActionResult> Search()
        {
            return await SearchByResourceType(type: null);
        }

        /// <summary>
        /// Searches for resources.
        /// </summary>
        [HttpPost]
        [Route(KnownRoutes.Search, Name = RouteNames.SearchAllResourcesPost)]
        [AuditEventSubType(AuditEventSubType.SearchSystem)]
        [Authorize(PolicyNames.ReadPolicy)]
        public async Task<IActionResult> SearchPost()
        {
            return await SearchByResourceTypePost(type: null);
        }

        /// <summary>
        /// Searches for resources.
        /// </summary>
        /// <param name="type">The resource type.</param>
        [HttpGet]
        [Route(KnownRoutes.ResourceType, Name = RouteNames.SearchResources)]
        [AuditEventSubType(AuditEventSubType.SearchType)]
        [Authorize(PolicyNames.ReadPolicy)]
        public async Task<IActionResult> SearchByResourceType(string type)
        {
            return await PerformSearch(type, GetQueriesForSearch());
        }

        private IReadOnlyList<Tuple<string, string>> GetQueriesForSearch()
        {
            IReadOnlyList<Tuple<string, string>> queries = Array.Empty<Tuple<string, string>>();

            if (Request.Query != null)
            {
                queries = Request.Query
                    .SelectMany(query => query.Value, (query, value) => Tuple.Create(query.Key, value)).ToArray();
            }

            return queries;
        }

        /// <summary>
        /// Searches for resources.
        /// </summary>
        /// <param name="type">The resource type.</param>
        [HttpPost]
        [Route(KnownRoutes.ResourceTypeSearch, Name = RouteNames.SearchResourcesPost)]
        [AuditEventSubType(AuditEventSubType.SearchType)]
        [Authorize(PolicyNames.ReadPolicy)]
        public async Task<IActionResult> SearchByResourceTypePost(string type)
        {
            var queries = new List<Tuple<string, string>>();

            AddItemsIfNotNull(Request.Query);

            if (Request.HasFormContentType)
            {
                AddItemsIfNotNull(Request.Form);
            }

            // TODO: In the case of POST, the server cannot use SelfLink to let client know which search parameter is not supported.
            // Therefore, it should throw an exception if an unsupported search parameter is encountered.
            return await PerformSearch(type, queries);

            void AddItemsIfNotNull(IEnumerable<KeyValuePair<string, StringValues>> source)
            {
                if (source != null)
                {
                    queries.AddRange(source.SelectMany(query => query.Value, (query, value) => Tuple.Create(query.Key, value)));
                }
            }
        }

        /// <summary>
        /// Searches by compartment.
        /// </summary>
        /// <param name="compartmentType">The compartment type.</param>
        /// <param name="id">The identifier.</param>
        /// <param name="type">The resource type.</param>
        [HttpGet]
        [Route(KnownRoutes.CompartmentTypeByResourceType, Name = RouteNames.SearchCompartmentByResourceType)]
        [AuditEventSubType(AuditEventSubType.Search)]
        public async Task<IActionResult> SearchCompartmentByResourceType(string compartmentType, string id, string type)
        {
            var queries = GetQueriesForSearch();
            return await PerformCompartmentSearch(compartmentType, id, type, queries);
        }

        private async Task<IActionResult> PerformCompartmentSearch(string compartmentType, string compartmentId, string resourceType, IReadOnlyList<Tuple<string, string>> queries)
        {
            Bundle response = await _mediator.SearchResourceCompartmentAsync(compartmentType, compartmentId, resourceType, queries, HttpContext.RequestAborted);

            return FhirResult.Create(response);
        }

        private async Task<IActionResult> PerformSearch(string type, IReadOnlyList<Tuple<string, string>> queries)
        {
            Bundle response = await _mediator.SearchResourceAsync(type, queries, HttpContext.RequestAborted);

            return FhirResult.Create(response);
        }

        /// <summary>
        /// Returns the Capability Statement of this server which is used to determine
        /// what FHIR features are supported by this implementation.
        /// </summary>
        /// <param name="system">Specifies if all system capabilities should be returned or only configured (default).</param>
        [HttpGet]
        [AllowAnonymous]
        [Route(KnownRoutes.Metadata, Name = RouteNames.Metadata)]
        public async Task<IActionResult> Metadata(bool system = false)
        {
            CapabilityStatement response = await _mediator.GetCapabilitiesAsync(system, HttpContext.RequestAborted);

            return FhirResult.Create(response);
        }
    }
}
