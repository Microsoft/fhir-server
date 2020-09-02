﻿// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EnsureThat;
using Hl7.Fhir.Model;
using MediatR;
using Microsoft.Health.Fhir.Core.Exceptions;
using Microsoft.Health.Fhir.Core.Extensions;
using Microsoft.Health.Fhir.Core.Features.Persistence;
using Microsoft.Health.Fhir.Core.Features.Search;
using Microsoft.Health.Fhir.Core.Features.Security;
using Microsoft.Health.Fhir.Core.Features.Security.Authorization;
using Microsoft.Health.Fhir.Core.Messages.Reindex;
using Microsoft.Health.Fhir.Core.Models;

namespace Microsoft.Health.Fhir.Core.Features.Operations.Reindex
{
    public class ReindexSingleResourceRequestHandler : IRequestHandler<ReindexSingleResourceRequest, ReindexSingleResourceResponse>
    {
        private readonly IFhirAuthorizationService _authorizationService;
        private readonly IFhirDataStore _fhirDataStore;
        private readonly ISearchIndexer _searchIndexer;
        private readonly IResourceDeserializer _resourceDeserializer;

        public ReindexSingleResourceRequestHandler(
            IFhirAuthorizationService authorizationService,
            IFhirDataStore fhirDataStore,
            ISearchIndexer searchIndexer,
            IResourceDeserializer deserializer)
        {
            EnsureArg.IsNotNull(authorizationService, nameof(authorizationService));
            EnsureArg.IsNotNull(fhirDataStore, nameof(fhirDataStore));
            EnsureArg.IsNotNull(searchIndexer, nameof(searchIndexer));
            EnsureArg.IsNotNull(deserializer, nameof(deserializer));

            _authorizationService = authorizationService;
            _fhirDataStore = fhirDataStore;
            _searchIndexer = searchIndexer;
            _resourceDeserializer = deserializer;
        }

        public async Task<ReindexSingleResourceResponse> Handle(ReindexSingleResourceRequest request, CancellationToken cancellationToken)
        {
            EnsureArg.IsNotNull(request, nameof(request));

            if (await _authorizationService.CheckAccess(DataActions.Reindex) != DataActions.Reindex)
            {
                throw new UnauthorizedFhirActionException();
            }

            var key = new ResourceKey(request.ResourceType, request.ResourceId);
            ResourceWrapper storedResource = await _fhirDataStore.GetAsync(key, cancellationToken);

            if (storedResource == null)
            {
                throw new ResourceNotFoundException(string.Format(Core.Resources.ResourceNotFoundById, request.ResourceType, request.ResourceId));
            }

            // We need to extract the "new" search indices since the assumption is that
            // a new search parameter has been added to the fhir server.
            ResourceElement resourceElement = _resourceDeserializer.Deserialize(storedResource);
            IReadOnlyCollection<SearchIndexEntry> newIndices = _searchIndexer.Extract(resourceElement);

            string searchIndexString = string.Empty;
            if (newIndices.Count > 0)
            {
                // A resource can have multiple values for the same search param (eg: name for Patient).
                // Hence using a HashSet to avoid duplicate values in the response.
                searchIndexString = string.Join(",", newIndices.Select(x => x.SearchParameter.Name).ToHashSet());
            }

            // Create a new parameter resource and include the new search indices.
            var parametersResource = new Parameters
            {
                Id = Guid.NewGuid().ToString(),
                VersionId = "1",
                Parameter = new List<Parameters.ParameterComponent>(),
            };
            parametersResource.Parameter.Add(new Parameters.ParameterComponent() { Name = "originalResourceId", Value = new FhirString(request.ResourceId) });
            parametersResource.Parameter.Add(new Parameters.ParameterComponent() { Name = "originalResourceType", Value = new FhirString(request.ResourceType) });
            parametersResource.Parameter.Add(new Parameters.ParameterComponent() { Name = "newSearchIndices", Value = new FhirString(searchIndexString) });

            return new ReindexSingleResourceResponse(parametersResource.ToResourceElement());
        }
    }
}
