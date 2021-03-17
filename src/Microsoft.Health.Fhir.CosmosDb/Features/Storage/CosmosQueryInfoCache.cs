﻿// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System;
using EnsureThat;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Health.Fhir.Core.Features.Search.Expressions;

namespace Microsoft.Health.Fhir.CosmosDb.Features.Storage
{
    public class CosmosQueryInfoCache
    {
        private readonly MemoryCache _cache = new(new MemoryCacheOptions { SizeLimit = 512 });

        internal QueryPartitionStatistics GetQueryPartitionStatistics(Expression expression)
        {
            return _cache.GetOrCreate(
                new ExpressionWrapper(expression),
                e =>
                {
                    e.Size = 1;
                    return new QueryPartitionStatistics();
                });
        }

        private class ExpressionWrapper
        {
            private readonly int _hashCode;

            public ExpressionWrapper(Expression expression)
            {
                EnsureArg.IsNotNull(expression, nameof(expression));

                Expression = expression;

                HashCode hashCode = default;

                Expression.AddValueInsensitiveHashCode(ref hashCode);
                _hashCode = hashCode.ToHashCode();
            }

            public Expression Expression { get; }

            public override int GetHashCode() => _hashCode;

            public override bool Equals(object obj) => obj is ExpressionWrapper e && Expression.ValueInsensitiveEquals(e.Expression);
        }
    }
}
