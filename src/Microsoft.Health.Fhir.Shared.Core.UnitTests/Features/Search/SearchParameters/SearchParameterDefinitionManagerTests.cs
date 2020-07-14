﻿// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Health.Fhir.Core.Features.Definition;
using Microsoft.Health.Fhir.Core.Features.Search.Parameters;
using Microsoft.Health.Fhir.Core.Features.Search.Registry;
using Microsoft.Health.Fhir.Core.Models;
using Microsoft.Health.Fhir.ValueSets;
using NSubstitute;
using Xunit;

namespace Microsoft.Health.Fhir.Core.UnitTests.Features.Search
{
    public class SearchParameterDefinitionManagerTests
    {
        private static readonly string ResourceId = "http://hl7.org/fhir/SearchParameter/Resource-id";
        private static readonly string ResourceLastUpdated = "http://hl7.org/fhir/SearchParameter/Resource-lastUpdated";
        private static readonly string ResourceProfile = "http://hl7.org/fhir/SearchParameter/Resource-profile";
        private static readonly string ResourceSecurity = "http://hl7.org/fhir/SearchParameter/Resource-security";
        private static readonly string ResourceQuery = "http://hl7.org/fhir/SearchParameter/Resource-query";

        private readonly SearchParameterStatusManager _manager;
        private readonly IStatusRegistryDataStore _statusRegistryDataStore;
        private readonly SearchParameterDefinitionManager _searchParameterDefinitionManager;
        private readonly IMediator _mediator;
        private readonly SearchParameterInfo[] _searchParameterInfos;
        private readonly SearchParameterInfo _queryParameter;
        private readonly ISearchParameterSupportResolver _searchParameterSupportResolver;

        public SearchParameterDefinitionManagerTests()
        {
            _searchParameterSupportResolver = Substitute.For<ISearchParameterSupportResolver>();
            _mediator = Substitute.For<IMediator>();
            _statusRegistryDataStore = Substitute.For<IStatusRegistryDataStore>();
            _searchParameterDefinitionManager = new SearchParameterDefinitionManager(ModelInfoProvider.Instance);

            _manager = new SearchParameterStatusManager(
                _statusRegistryDataStore,
                _searchParameterDefinitionManager,
                _searchParameterSupportResolver,
                _mediator);

            _statusRegistryDataStore.GetSearchParameterStatuses()
                .Returns(new[]
                {
                    new ResourceSearchParameterStatus
                    {
                        Status = SearchParameterStatus.Enabled,
                        Uri = new Uri(ResourceId),
                    },
                    new ResourceSearchParameterStatus
                    {
                        Status = SearchParameterStatus.Enabled,
                        Uri = new Uri(ResourceLastUpdated),
                        IsPartiallySupported = true,
                    },
                    new ResourceSearchParameterStatus
                    {
                        Status = SearchParameterStatus.Disabled,
                        Uri = new Uri(ResourceProfile),
                    },
                    new ResourceSearchParameterStatus
                    {
                        Status = SearchParameterStatus.Supported,
                        Uri = new Uri(ResourceSecurity),
                    },
                });

            _queryParameter = new SearchParameterInfo("_query", SearchParamType.Token, new Uri(ResourceQuery));
            _searchParameterInfos = new[]
            {
                new SearchParameterInfo("_id", SearchParamType.Token, new Uri(ResourceId)),
                new SearchParameterInfo("_lastUpdated", SearchParamType.Token, new Uri(ResourceLastUpdated)),
                new SearchParameterInfo("_profile", SearchParamType.Token, new Uri(ResourceProfile)),
                new SearchParameterInfo("_security", SearchParamType.Token, new Uri(ResourceSecurity)),
                _queryParameter,
            };

            _searchParameterSupportResolver
                .IsSearchParameterSupported(Arg.Any<SearchParameterInfo>())
                .Returns((false, false));

            _searchParameterSupportResolver
                .IsSearchParameterSupported(Arg.Is(_searchParameterInfos[4]))
                .Returns((true, false));
        }

        [Fact]
        public async Task GivenSupportedParams_WhenGettingSupported_ThenSupportedParamsReturned()
        {
            _searchParameterDefinitionManager.Start();
            await _manager.EnsureInitialized();
            var supportedDefinitionManager = new SupportedSearchParameterDefinitionManager(_searchParameterDefinitionManager);
            var paramList = supportedDefinitionManager.GetSupportedButNotSearchableParams();

            Assert.Single(paramList);
            Assert.Collection(paramList, p =>
            {
                Assert.True(p.IsSupported);
                Assert.False(p.IsSearchable);
            });
        }

        [Fact]
        public async Task GivenSearchableParams_WhenGettingSearchable_ThenCorrectParamsReturned()
        {
            _searchParameterDefinitionManager.Start();
            await _manager.EnsureInitialized();
            var searchableDefinitionManager = new SearchableSearchParameterDefinitionManager(_searchParameterDefinitionManager);
            var paramList = searchableDefinitionManager.AllSearchParameters;

            Assert.Collection(
                paramList,
                p =>
                {
                    Assert.True(p.IsSupported);
                    Assert.True(p.IsSearchable);
                },
                p2 =>
                {
                    Assert.True(p2.IsSupported);
                    Assert.True(p2.IsSearchable);
                });
        }
    }
}
