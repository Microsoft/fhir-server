﻿// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System.Collections.Generic;
using Microsoft.Health.Fhir.Core.Features;
using Microsoft.Health.Fhir.Core.Features.Search;
using Microsoft.Health.Fhir.Core.Features.Search.Expressions;

namespace Microsoft.Health.Fhir.SqlServer.Features.Search.Expressions.Visitors
{
    /// A visitor for a Sort paramater.
    /// It creates the correct Generator and populates the normalized predicates.
    internal class SortRewriter : SqlExpressionRewriter<SearchOptions>
    {
        private readonly NormalizedSearchParameterQueryGeneratorFactory _normalizedSearchParameterQueryGeneratorFactory;

        public SortRewriter(NormalizedSearchParameterQueryGeneratorFactory normalizedSearchParameterQueryGeneratorFactory)
        {
            _normalizedSearchParameterQueryGeneratorFactory = normalizedSearchParameterQueryGeneratorFactory;
        }

        public override Expression VisitSqlRoot(SqlRootExpression expression, SearchOptions context)
        {
            if (context.CountOnly)
            {
                return expression;
            }

            // Proceed if we sort params were requested.
            if (context.Sort.Count == 0)
            {
                return expression;
            }

            // _lastUpdated sort param is handled differently than others, because it can be
            // inferred directly from the resource table itself.
            if (context.Sort[0].searchParameterInfo.Name == KnownQueryParameterNames.LastUpdated)
            {
                return expression;
            }

            var queryGenerator = _normalizedSearchParameterQueryGeneratorFactory.GetNormalizedSearchParameterQueryGenerator(context.Sort[0].searchParameterInfo);

            var newNormalizedPredicates = new List<TableExpression>(expression.TableExpressions.Count + 1);
            newNormalizedPredicates.AddRange(expression.TableExpressions);

            newNormalizedPredicates.Add(new TableExpression(queryGenerator, new SortExpression(context.Sort[0].searchParameterInfo), null, TableExpressionKind.Sort));

            return new SqlRootExpression(newNormalizedPredicates, expression.DenormalizedExpressions);
        }
    }
}
