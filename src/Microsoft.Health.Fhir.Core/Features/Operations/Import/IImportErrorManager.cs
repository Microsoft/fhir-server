﻿// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Microsoft.Health.Fhir.Core.Features.Operations.Import
{
    public interface IImportErrorManager
    {
        public Task<(long count, Uri fileUri)> HandleImportErrorAsync(string fileName, Channel<BatchProcessErrorRecord> errorsChannel, long startErrorLogBatchId, Action<long, long> progressUpdater, CancellationToken cancellationToken);
    }
}