﻿// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using Microsoft.Health.Fhir.Core.Features.Search.Expressions;
using Microsoft.Health.Fhir.SqlServer.Features.Search.Expressions.Visitors;
using Microsoft.Health.Fhir.SqlServer.Features.Search.Expressions.Visitors.QueryGenerators;

namespace Microsoft.Health.Fhir.SqlServer.Features.Search.Expressions
{
    /// <summary>
    /// An expression over a search param or compartment table.
    /// </summary>
    internal class TableExpression : Expression
    {
        /// <summary>
        /// Creates a new instance of the <see cref="TableExpression"/> class.
        /// </summary>
        /// <param name="queryGenerator">The search parameter query generator</param>
        /// <param name="predicate">The search expression over a columns belonging exclusively to a search parameter table.
        /// Applies to the chain target if a chained expression.</param>
        /// <param name="kind">The table expression kind.</param>
        /// <param name="chainLevel">The nesting chain nesting level of the current expression. 0 if not a chain expression.</param>
        public TableExpression(
            TableExpressionQueryGenerator queryGenerator,
            Expression predicate,
            TableExpressionKind kind,
            int chainLevel = 0)
        {
            QueryGenerator = queryGenerator;
            Predicate = predicate;
            Kind = kind;
            ChainLevel = chainLevel;
        }

        public TableExpressionKind Kind { get; }

        /// <summary>
        /// The nesting chain nesting level of the current expression. 0 if not a chain expression.
        /// </summary>
        public int ChainLevel { get; }

        public TableExpressionQueryGenerator QueryGenerator { get; }

        /// <summary>
        /// The search expression over a columns belonging exclusively to a search parameter table.
        /// Applies to the chain target if a chained expression.
        /// </summary>
        public Expression Predicate { get; }

        public override TOutput AcceptVisitor<TContext, TOutput>(IExpressionVisitor<TContext, TOutput> visitor, TContext context)
        {
            return AcceptVisitor((ISqlExpressionVisitor<TContext, TOutput>)visitor, context);
        }

        public TOutput AcceptVisitor<TContext, TOutput>(ISqlExpressionVisitor<TContext, TOutput> visitor, TContext context)
        {
            return visitor.VisitTable(this, context);
        }

        public override string ToString()
        {
            return $"(Table {Kind} {(ChainLevel == 0 ? null : $"ChainLevel:{ChainLevel} ")}{QueryGenerator?.Table} Normalized:{Predicate})";
        }
    }
}
