﻿// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EnsureThat;
using Hl7.Fhir.ElementModel;
using Hl7.Fhir.Model;
using MediatR;
using Microsoft.Health.Fhir.Core.Extensions;
using Microsoft.Health.Fhir.Core.Features.Conformance;
using Microsoft.Health.Fhir.Core.Features.Persistence;
using Microsoft.Health.Fhir.Core.Features.Search;
using Microsoft.Health.Fhir.Core.Messages.Create;
using Microsoft.Health.Fhir.Core.Messages.Upsert;

namespace Microsoft.Health.Fhir.Core.Features.Resources.Create
{
    public class CreateResourceHandler : BaseConditionalHandler, IRequestHandler<CreateResourceRequest, UpsertResourceResponse>
    {
        private readonly Dictionary<string, (string resourceId, string resourceType)> _referenceIdDictionary;

        public CreateResourceHandler(
            IFhirDataStore fhirDataStore,
            Lazy<IConformanceProvider> conformanceProvider,
            IResourceWrapperFactory resourceWrapperFactory,
            ISearchService searchService,
            ResourceIdProvider resourceIdProvider)
            : base(fhirDataStore, searchService, conformanceProvider, resourceWrapperFactory, resourceIdProvider)
        {
            _referenceIdDictionary = new Dictionary<string, (string resourceId, string resourceType)>();
        }

        public async Task<UpsertResourceResponse> Handle(CreateResourceRequest message, CancellationToken cancellationToken)
        {
            EnsureArg.IsNotNull(message, nameof(message));

            var resource = message.Resource.Instance.ToPoco<Resource>();

            // If an Id is supplied on create it should be removed/ignored
            resource.Id = null;

            await ResolveBundleReferencesAsync(resource, _referenceIdDictionary, resource.ResourceType.ToString(), cancellationToken);

            ResourceWrapper resourceWrapper = CreateResourceWrapper(resource, deleted: false);

            bool keepHistory = await ConformanceProvider.Value.CanKeepHistory(resource.TypeName, cancellationToken);

            UpsertOutcome result = await FhirDataStore.UpsertAsync(
                resourceWrapper,
                weakETag: null,
                allowCreate: true,
                keepHistory: keepHistory,
                cancellationToken: cancellationToken);

            resource.VersionId = result.Wrapper.Version;

            return new UpsertResourceResponse(new SaveOutcome(resource.ToResourceElement(), SaveOutcomeType.Created));
        }

        private static IEnumerable<ResourceReference> ResourceRefUrl(Resource resource)
        {
            foreach (Base child in resource.Children)
            {
                if (child is ResourceReference targetTypeObject)
                {
                    yield return targetTypeObject;
                }
            }
        }
    }
}
