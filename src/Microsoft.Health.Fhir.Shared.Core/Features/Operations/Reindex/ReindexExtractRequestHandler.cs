﻿// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System.Threading;
using System.Threading.Tasks;
using EnsureThat;
using MediatR;
using Microsoft.Health.Fhir.Core.Messages.Reindex;

namespace Microsoft.Health.Fhir.Core.Features.Operations.Reindex
{
    public class ReindexExtractRequestHandler : INotificationHandler<ExtractReindexRequest>
    {
        private readonly ReindexUtilities _reindexUtilities;

        public ReindexExtractRequestHandler(ReindexUtilities reindexUtilities)
        {
            EnsureArg.IsNotNull(reindexUtilities, nameof(reindexUtilities));

            _reindexUtilities = reindexUtilities;
        }

        public async Task Handle(ExtractReindexRequest notification, CancellationToken cancellationToken)
        {
            await _reindexUtilities.ProcessSearchResultsAsync(notification.Result, notification.HashValue, cancellationToken);
        }
    }
}
