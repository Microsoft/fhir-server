﻿// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Primitives;
using Microsoft.Health.Fhir.Core.Exceptions;
using Microsoft.Health.Fhir.Core.Features.Conformance;
using Microsoft.Health.Fhir.Core.Features.Persistence;
using Microsoft.Health.Fhir.Core.Features.Resources;
using Microsoft.Health.Fhir.Core.Features.Search;
using Microsoft.Health.Fhir.Core.Messages.Search;
using Microsoft.Health.Fhir.Core.Models;
using static Hl7.Fhir.Model.Bundle;

namespace Microsoft.Health.Fhir.Api.Features.Resources.Bundle
{
    public class TransactionValidator : BaseConditionalHandler
    {
        public TransactionValidator(
            IFhirDataStore fhirDataStore,
            Lazy<IConformanceProvider> conformanceProvider,
            IResourceWrapperFactory resourceWrapperFactory,
            ISearchService searchService,
            ResourceIdProvider resourceIdProvider)
            : base(fhirDataStore, searchService, conformanceProvider, resourceWrapperFactory, resourceIdProvider)
        {
        }

        /// <summary>
        /// Validates if transaction bundle contains multiple entries that are modifying the same resource.
        /// </summary>
        /// <param name="bundle"> The input bundle</param>
        public async Task ValidateBundle(Hl7.Fhir.Model.Bundle bundle)
        {
            if (bundle.Type != BundleType.Transaction)
            {
                return;
            }

            var resourceIdList = new HashSet<string>(StringComparer.Ordinal);

            foreach (var entry in bundle.Entry)
            {
                if (ShouldValidateBundleEntry(entry))
                {
                    string resourceId = await GetResourceId(entry);
                    string conditionalCreateQuery = entry.Request.IfNoneExist ?? entry.Request.IfNoneExist;

                    if (!string.IsNullOrEmpty(resourceId))
                    {
                        // Throw exception if resourceId is already present in the hashset.
                        if (resourceIdList.Contains(resourceId))
                        {
                            string requestUrl = BuildRequestUrlForConditionalQueries(entry, conditionalCreateQuery);
                            throw new RequestNotValidException(string.Format(Api.Resources.ResourcesMustBeUnique, requestUrl));
                        }

                        resourceIdList.Add(resourceId);
                    }
                }
            }
        }

        private static string BuildRequestUrlForConditionalQueries(EntryComponent entry, string conditionalCreateQuery)
        {
            return conditionalCreateQuery == null ? entry.Request.Url : entry.Request.Url + "?" + conditionalCreateQuery;
        }

        private async Task<string> GetResourceId(EntryComponent entry)
        {
            if (entry.Request.IfNoneExist == null && !entry.Request.Url.Contains("?", StringComparison.InvariantCulture))
            {
                if (entry.Request.Method == HTTPVerb.POST)
                {
                    return entry.FullUrl;
                }

                return entry.Request.Url;
            }
            else
            {
                string resourceType = null;
                StringValues conditionalQueries;
                HTTPVerb requestMethod = (HTTPVerb)entry.Request.Method;
                bool conditionalCreate = requestMethod == HTTPVerb.POST;
                bool condtionalUpdate = requestMethod == HTTPVerb.PUT || requestMethod == HTTPVerb.DELETE;

                if (condtionalUpdate)
                {
                    string[] conditinalUpdateParameters = entry.Request.Url.Split("?");
                    resourceType = conditinalUpdateParameters[0];
                    conditionalQueries = conditinalUpdateParameters[1];
                }
                else if (conditionalCreate)
                {
                    resourceType = entry.Request.Url;
                    conditionalQueries = entry.Request.IfNoneExist;
                }

                SearchResultEntry[] matchedResults = await GetExistingResourceId(entry.Request.Url, resourceType, conditionalQueries);
                int? count = matchedResults?.Length;

                if (count > 1)
                {
                    // Multiple matches: The server returns a 412 Precondition Failed error indicating the client's criteria were not selective enough
                    throw new PreconditionFailedException(string.Format(Api.Resources.ConditionalOperationNotSelectiveEnough, conditionalQueries));
                }

                if (count == 1)
                {
                    return entry.Resource.TypeName + "/" + matchedResults[0].Resource.ResourceId;
                }
            }

            return string.Empty;
        }

        public async Task<SearchResultEntry[]> GetExistingResourceId(string requestUrl, string resourceType, StringValues conditionalQueries)
        {
            if (string.IsNullOrEmpty(resourceType) || string.IsNullOrEmpty(conditionalQueries))
            {
                throw new RequestNotValidException(string.Format(Api.Resources.InvalidConditionalReferenceParameters, requestUrl));
            }

            Tuple<string, string>[] conditionalParameters = QueryHelpers.ParseQuery(conditionalQueries)
                              .SelectMany(query => query.Value, (query, value) => Tuple.Create(query.Key, value)).ToArray();

            var searchResourceRequest = new SearchResourceRequest(resourceType, conditionalParameters);

            return await Search(searchResourceRequest.ResourceType, searchResourceRequest.Queries, CancellationToken.None);
        }

        private static bool ShouldValidateBundleEntry(EntryComponent entry)
        {
            string requestUrl = entry.Request.Url;
            HTTPVerb? requestMethod = entry.Request.Method;

            // Search operations using _search and POST endpoint is not supported for bundle.
            if (requestMethod == HTTPVerb.POST && requestUrl.Contains("_search", StringComparison.InvariantCulture))
            {
                throw new RequestNotValidException(string.Format(Api.Resources.InvalidBundleEntry, entry.Request.Url));
            }

            // Resource type bundle is not supported.within a bundle.
            if (entry.Resource?.ResourceType == Hl7.Fhir.Model.ResourceType.Bundle)
            {
                throw new RequestNotValidException(string.Format(Api.Resources.UnsupportedResourceType, KnownResourceTypes.Bundle));
            }

            // Check for duplicate resources within a bundle entry is skipped if the request within a entry is not modifying the resource.
            return !(requestMethod == HTTPVerb.GET
                    || requestUrl.Contains("$", StringComparison.InvariantCulture));
        }
    }
}
