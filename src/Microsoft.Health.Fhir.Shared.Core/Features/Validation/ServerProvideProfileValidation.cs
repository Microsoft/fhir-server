﻿// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EnsureThat;
using Hl7.Fhir.ElementModel;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using Hl7.Fhir.Specification.Source;
using Hl7.Fhir.Specification.Summary;
using MediatR;
using Microsoft.Extensions.Options;
using Microsoft.Health.Core.Extensions;
using Microsoft.Health.Extensions.DependencyInjection;
using Microsoft.Health.Fhir.Core.Configs;
using Microsoft.Health.Fhir.Core.Features.Search;
using Microsoft.Health.Fhir.Core.Messages.CapabilityStatement;

namespace Microsoft.Health.Fhir.Core.Features.Validation
{
    /// <summary>
    /// Provides profiles by fetching them from the server.
    /// </summary>
    public sealed class ServerProvideProfileValidation : IProvideProfilesForValidation
    {
        private static HashSet<string> _supportedTypes = new HashSet<string>() { "ValueSet", "StructureDefinition", "CodeSystem" };
        private readonly Func<IScoped<ISearchService>> _searchServiceFactory;
        private readonly ValidateOperationConfiguration _validateOperationConfig;
        private readonly IMediator _mediator;
        private List<ArtifactSummary> _summaries;
        private DateTime _expirationTime;
        private object _lockSummaries = new object();

        public ServerProvideProfileValidation(
            Func<IScoped<ISearchService>> searchServiceFactory,
            IOptions<ValidateOperationConfiguration> options,
            IMediator mediator)
        {
            EnsureArg.IsNotNull(searchServiceFactory, nameof(searchServiceFactory));
            EnsureArg.IsNotNull(options?.Value, nameof(options));
            EnsureArg.IsNotNull(mediator, nameof(mediator));

            _searchServiceFactory = searchServiceFactory;
            _expirationTime = DateTime.UtcNow;
            _validateOperationConfig = options.Value;
            _mediator = mediator;
        }

        public IReadOnlySet<string> GetProfilesTypes() => _supportedTypes;

        public IEnumerable<ArtifactSummary> ListSummaries(bool resetStatementIfNew = true)
        {
            lock (_lockSummaries)
            {
                if (DateTime.UtcNow >= _expirationTime)
                {
                    var oldHash = resetStatementIfNew ? GetHashForSupportedProfiles(_summaries) : string.Empty;
                    var result = System.Threading.Tasks.Task.Run(() => GetSummaries()).GetAwaiter().GetResult();
                    _summaries = result;
                    var newHash = resetStatementIfNew ? GetHashForSupportedProfiles(_summaries) : string.Empty;
                    _expirationTime = DateTime.UtcNow.AddSeconds(_validateOperationConfig.CacheDurationInSeconds);

                    if (newHash != oldHash)
                    {
                        _mediator.Publish(new RebuildCapabilityStatement());
                    }
                }

                return _summaries;
            }
        }

        private async Task<List<ArtifactSummary>> GetSummaries()
        {
            var result = new List<ArtifactSummary>();
            using (IScoped<ISearchService> searchService = _searchServiceFactory())
            {
                foreach (var type in _supportedTypes)
                {
                    string ct = null;
                    {
                        do
                        {
                            var queryParameters = new List<Tuple<string, string>>();
                            if (ct != null)
                            {
                                ct = Convert.ToBase64String(Encoding.UTF8.GetBytes(ct));
                                queryParameters.Add(new Tuple<string, string>(KnownQueryParameterNames.ContinuationToken, ct));
                            }

                            var searchResult = await searchService.Value.SearchAsync(type, queryParameters, CancellationToken.None);
                            foreach (var searchItem in searchResult.Results)
                            {
                                using (var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(searchItem.Resource.RawResource.Data)))
                                {
                                    var navStream = new JsonNavigatorStream(memoryStream);
                                    Action<ArtifactSummaryPropertyBag> setOrigin =
                                        (properties) =>
                                        {
                                            properties[ArtifactSummaryProperties.OriginKey] = searchItem.Resource.RawResource.Data;
                                        };
                                    var artifacts = ArtifactSummaryGenerator.Default.Generate(navStream, setOrigin);
                                    result.AddRange(artifacts);
                                }
                            }

                            ct = searchResult.ContinuationToken;
                        }
                        while (ct != null);
                    }
                }

                return result;
            }
        }

        private static Resource LoadBySummary(ArtifactSummary summary)
        {
            if (summary == null)
            {
                return null;
            }

            using (var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(summary.Origin)))
            using (var navStream = new JsonNavigatorStream(memoryStream))
            {
                if (navStream.Seek(summary.Position))
                {
                    if (navStream.Current != null)
                    {
                        // TODO: Cache this parsed resource, to prevent parsing again and again
                        var resource = navStream.Current.ToPoco<Resource>();
                        return resource;
                    }
                }
            }

            return null;
        }

        public Resource ResolveByCanonicalUri(string uri)
        {
            var summary = ListSummaries().ResolveByCanonicalUri(uri);
            return LoadBySummary(summary);
        }

        public Resource ResolveByUri(string uri)
        {
            var summary = ListSummaries().ResolveByUri(uri);
            return LoadBySummary(summary);
        }

        public IEnumerable<string> GetSupportedProfiles(string resourceType)
        {
            var summary = ListSummaries(false);
            return summary.Where(x => x.ResourceType == ResourceType.StructureDefinition)
                .Where(x =>
                    {
                        if (!x.TryGetValue(StructureDefinitionSummaryProperties.TypeKey, out object type))
                        {
                            return false;
                        }

                        return string.Equals((string)type, resourceType, StringComparison.OrdinalIgnoreCase);
                    })
                .Select(x => x.ResourceUri).ToList();
        }

        private static string GetHashForSupportedProfiles(IEnumerable<ArtifactSummary> summaries)
        {
            if (summaries == null)
            {
                return string.Empty;
            }

            var sb = new StringBuilder();
            summaries.Where(x => x.ResourceType == ResourceType.StructureDefinition)
               .Where(x => x.TryGetValue(StructureDefinitionSummaryProperties.TypeKey, out object type))
               .Select(x => x.ResourceUri).ToList().ForEach(url => sb.Append(url));

            return sb.ToString().ComputeHash();
        }
    }
}
