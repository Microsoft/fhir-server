// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using EnsureThat;
using Microsoft.Health.Fhir.Core.Models;

namespace Microsoft.Health.Fhir.Core.Features.Search.Expressions
{
    /// <summary>
    /// Represents an include/revinclude expression (where an additional resource is included based on a reference)
    /// </summary>
    public abstract class IncludeBaseExpression : Expression
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="IncludeBaseExpression"/> class.
        /// </summary>
        /// <param name="resourceType">The resource that supports the reference.</param>
        /// <param name="referenceSearchParameter">THe search parameter that establishes the reference relationship.</param>
        /// <param name="targetResourceType">The target type of the reference.</param>
        /// <param name="wildCard">If this is a wildcard reference include (include all referenced resources).</param>
        public IncludeBaseExpression(string resourceType, SearchParameterInfo referenceSearchParameter, string targetResourceType, bool wildCard)
        {
            EnsureArg.IsNotNullOrWhiteSpace(resourceType, nameof(resourceType));

            if (!wildCard)
            {
                EnsureArg.IsNotNull(referenceSearchParameter, nameof(referenceSearchParameter));
            }

            ResourceType = resourceType;
            ReferenceSearchParameter = referenceSearchParameter;
            TargetResourceType = targetResourceType;
            WildCard = wildCard;
        }

        /// <summary>
        /// Gets the resource type which is being searched.
        /// </summary>
        public string ResourceType { get; }

        /// <summary>
        /// Gets the reference search parameter for the relationship.
        /// </summary>
        public SearchParameterInfo ReferenceSearchParameter { get; }

        /// <summary>
        /// Gets the target resource type. Value will be null if none are specified.
        /// </summary>
        public string TargetResourceType { get; }

        /// <summary>
        /// Gets if the include is a wildcard include.
        /// </summary>
        public bool WildCard { get; }
    }
}
