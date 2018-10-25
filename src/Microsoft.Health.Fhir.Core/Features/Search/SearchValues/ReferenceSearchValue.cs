﻿// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System;
using EnsureThat;
using Hl7.Fhir.Model;

namespace Microsoft.Health.Fhir.Core.Features.Search.SearchValues
{
    /// <summary>
    /// Represents a reference search value.
    /// </summary>
    public class ReferenceSearchValue : ISearchValue
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ReferenceSearchValue"/> class.
        /// </summary>
        /// <param name="referenceKind">The kind of reference.</param>
        /// <param name="baseUri">The base URI of the resource.</param>
        /// <param name="resourceType">The resource type.</param>
        /// <param name="resourceId">The resource id.</param>
        public ReferenceSearchValue(ReferenceKind referenceKind, Uri baseUri, ResourceType? resourceType, string resourceId)
        {
            if (baseUri != null)
            {
                EnsureArg.IsNotNull(resourceType, nameof(resourceType));
            }

            EnsureArg.IsNotNullOrWhiteSpace(resourceId, nameof(resourceId));

            Kind = referenceKind;
            BaseUri = baseUri;
            ResourceType = resourceType;
            ResourceId = resourceId;
        }

        /// <summary>
        /// Gets the kind of reference.
        /// </summary>
        public ReferenceKind Kind { get; }

        /// <summary>
        /// Gets the base URI of the resource.
        /// </summary>
        public Uri BaseUri { get; }

        /// <summary>
        /// Gets the resource type.
        /// </summary>
        public ResourceType? ResourceType { get; }

        /// <summary>
        /// Gets the resource id.
        /// </summary>
        public string ResourceId { get; }

        /// <inheritdoc />
        public bool IsValidAsCompositeComponent => true;

        /// <inheritdoc />
        public void AcceptVisitor(ISearchValueVisitor visitor)
        {
            EnsureArg.IsNotNull(visitor, nameof(visitor));

            visitor.Visit(this);
        }

        /// <inheritdoc />
        public override string ToString()
        {
            if (BaseUri != null)
            {
                return $"{BaseUri}{ResourceType}/{ResourceId}";
            }
            else if (ResourceType == null)
            {
                return ResourceId;
            }

            return $"{ResourceType}/{ResourceId}";
        }
    }
}
