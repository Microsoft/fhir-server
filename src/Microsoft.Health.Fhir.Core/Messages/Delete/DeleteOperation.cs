﻿// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

namespace Microsoft.Health.Fhir.Core.Messages.Delete
{
    public enum DeleteOperation
    {
        SoftDelete,
        HardDelete,
        Purge,
    }
}
