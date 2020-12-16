﻿// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using EnsureThat;
using Microsoft.Health.Fhir.Core.Features.Search.Expressions;
using Microsoft.Health.Fhir.SqlServer.Features.Search.Expressions.Visitors.QueryGenerators;

namespace Microsoft.Health.Fhir.SqlServer.Features.Search.Expressions.Visitors
{
    /// <summary>
    /// Constructs a <see cref="SqlRootExpression"/> by partitioning predicates into normalized and denormalized predicates.
    /// </summary>
    internal class SqlRootExpressionRewriter : ExpressionRewriterWithInitialContext<int>
    {
        private readonly NormalizedSearchParameterQueryGeneratorFactory _normalizedSearchParameterQueryGeneratorFactory;

        public SqlRootExpressionRewriter(NormalizedSearchParameterQueryGeneratorFactory normalizedSearchParameterQueryGeneratorFactory)
        {
            EnsureArg.IsNotNull(normalizedSearchParameterQueryGeneratorFactory, nameof(normalizedSearchParameterQueryGeneratorFactory));
            _normalizedSearchParameterQueryGeneratorFactory = normalizedSearchParameterQueryGeneratorFactory;
        }

        public override Expression VisitMultiary(MultiaryExpression expression, int context)
        {
            if (expression.MultiaryOperation != MultiaryOperator.And)
            {
                throw new InvalidOperationException("Or is not supported as a top-level expression");
            }

            List<SearchParameterExpressionBase> denormalizedPredicates = null;
            List<TableExpression> normalizedPredicates = null;

            for (var i = 0; i < expression.Expressions.Count; i++)
            {
                Expression childExpression = expression.Expressions[i];

                if (TryGetNormalizedGenerator(childExpression, out var normalizedGenerator, out var tableExpressionKind))
                {
                    EnsureAllocatedAndPopulatedChangeType(ref denormalizedPredicates, expression.Expressions, i);
                    EnsureAllocatedAndPopulated(ref normalizedPredicates, Array.Empty<TableExpression>(), 0);

                    normalizedPredicates.Add(new TableExpression(normalizedGenerator, childExpression, null, tableExpressionKind, tableExpressionKind == TableExpressionKind.Chain ? 1 : 0));
                }
                else
                {
                    denormalizedPredicates?.Add((SearchParameterExpressionBase)childExpression);
                }
            }

            if (normalizedPredicates == null)
            {
                denormalizedPredicates = new List<SearchParameterExpressionBase>(expression.Expressions.Count);
                foreach (Expression resourceExpression in expression.Expressions)
                {
                    denormalizedPredicates.Add((SearchParameterExpressionBase)resourceExpression);
                }

                return SqlRootExpression.WithResourceExpressions(denormalizedPredicates);
            }

            if (denormalizedPredicates == null)
            {
                return SqlRootExpression.WithTableExpressions(normalizedPredicates);
            }

            return new SqlRootExpression(normalizedPredicates, denormalizedPredicates);
        }

        public override Expression VisitSearchParameter(SearchParameterExpression expression, int context) => ConvertNonMultiary(expression);

        public override Expression VisitCompartment(CompartmentSearchExpression expression, int context) => ConvertNonMultiary(expression);

        public override Expression VisitMissingSearchParameter(MissingSearchParameterExpression expression, int context) => ConvertNonMultiary(expression);

        public override Expression VisitChained(ChainedExpression expression, int context)
        {
            return ConvertNonMultiary(expression);
        }

        private Expression ConvertNonMultiary(Expression expression)
        {
            return TryGetNormalizedGenerator(expression, out var generator, out var kind)
                ? SqlRootExpression.WithTableExpressions(new TableExpression(generator, normalizedPredicate: expression, denormalizedPredicate: null, kind, chainLevel: kind == TableExpressionKind.Chain ? 1 : 0))
                : SqlRootExpression.WithResourceExpressions((SearchParameterExpressionBase)expression);
        }

        private bool TryGetNormalizedGenerator(Expression expression, out NormalizedSearchParameterQueryGenerator normalizedGenerator, out TableExpressionKind kind)
        {
            normalizedGenerator = expression.AcceptVisitor(_normalizedSearchParameterQueryGeneratorFactory);
            switch (normalizedGenerator)
            {
                case ChainAnchorQueryGenerator _:
                    kind = TableExpressionKind.Chain;
                    break;
                case IncludeQueryGenerator _:
                    kind = TableExpressionKind.Include;
                    break;
                default:
                    kind = TableExpressionKind.Normal;
                    break;
            }

            return normalizedGenerator != null;
        }
    }
}
