﻿// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using EnsureThat;
using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using Microsoft.Extensions.Logging;
using Microsoft.Health.Fhir.Core.Features.Search.Expressions;
using Microsoft.Health.Fhir.Core.Features.Search.Expressions.Parsers;

namespace Microsoft.Health.Fhir.Core.Features.Search
{
    public class SearchOptionsFactory : ISearchOptionsFactory
    {
        private readonly IExpressionParser _expressionParser;
        private readonly ILogger _logger;

        public SearchOptionsFactory(
            IExpressionParser expressionParser,
            ILogger<SearchOptionsFactory> logger)
        {
            EnsureArg.IsNotNull(expressionParser, nameof(expressionParser));
            EnsureArg.IsNotNull(logger, nameof(logger));

            _expressionParser = expressionParser;
            _logger = logger;
        }

        public SearchOptions Create(string resourceType, IReadOnlyList<Tuple<string, string>> queryParameters)
        {
            return Create(new SearchOptions(resourceType), queryParameters);
        }

        public SearchOptions Create(IReadOnlyList<Tuple<string, string>> queryParameters)
        {
            return Create(new SearchOptions(), queryParameters);
        }

        private SearchOptions Create(SearchOptions options, IReadOnlyList<Tuple<string, string>> queryParameters)
        {
            EnsureArg.IsNotNull(options, nameof(options));

            string continuationToken = null;

            var searchParams = new SearchParams();
            var unsupportedSearchParameters = new List<Tuple<string, string>>();

            // Extract the continuation token, filter out the other known query parameters that's not search related.
            foreach (Tuple<string, string> query in queryParameters ?? Enumerable.Empty<Tuple<string, string>>())
            {
                if (query.Item1 == KnownQueryParameterNames.ContinuationToken)
                {
                    if (continuationToken != null)
                    {
                        throw new InvalidSearchOperationException(
                            string.Format(Core.Resources.MultipleQueryParametersNotAllowed, KnownQueryParameterNames.ContinuationToken));
                    }

                    continuationToken = query.Item2;
                }
                else if (query.Item1 == KnownQueryParameterNames.Format)
                {
                    // TODO: We need to handle format parameter.
                }
                else if (string.IsNullOrWhiteSpace(query.Item2))
                {
                    // Query parameter with empty value is not supported.
                    unsupportedSearchParameters.Add(query);
                }
                else
                {
                    // Parse the search parameters.
                    try
                    {
                        searchParams.Add(query.Item1, query.Item2);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogInformation(ex, "Failed to parse the query parameter. Skipping.");

                        // There was a problem parsing the parameter. Add it to list of unsupported parameters.
                        unsupportedSearchParameters.Add(query);
                    }
                }
            }

            options.ContinuationToken = continuationToken;

            // Check the item count.
            if (searchParams.Count != null)
            {
                options.MaxItemCount = searchParams.Count.Value;
            }

            // Check to see if only the count should be returned
            options.CountOnly = searchParams.Summary == SummaryType.Count;

            // If the resource type is not specified, then the common
            // search parameters should be used.
            ResourceType resourceType = ResourceType.DomainResource;

            if (options.ResourceType != null &&
                !Enum.TryParse(options.ResourceType, out resourceType))
            {
                throw new ResourceNotSupportedException(options.ResourceType);
            }

            if (searchParams.Parameters.Any())
            {
                // Convert the search parameter into expressions.
                Expression[] searchExpressions = searchParams.Parameters.Select(
                    q =>
                    {
                        try
                        {
                            return _expressionParser.Parse(resourceType, q.Item1, q.Item2);
                        }
                        catch (SearchParameterNotSupportedException)
                        {
                            unsupportedSearchParameters.Add(q);

                            return null;
                        }
                    })
                    .Where(item => item != null)
                    .ToArray();

                if (searchExpressions.Any())
                {
                    MultiaryExpression expression = Expression.And(searchExpressions);

                    options.Expression = expression;
                }
            }

            if (unsupportedSearchParameters.Any())
            {
                // TODO: Client can specify whether exception should be raised or not when it encounters unknown search parameters.
                // For now, we will ignore any unknown search parameters.
            }

            options.UnsupportedSearchParams = unsupportedSearchParameters;

            return options;
        }
    }
}
