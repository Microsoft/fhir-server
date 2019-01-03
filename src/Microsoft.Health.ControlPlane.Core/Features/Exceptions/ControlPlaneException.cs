﻿// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

namespace Microsoft.Health.ControlPlane.Core.Features.Exceptions
{
    public abstract class ControlPlaneException : Abstractions.Exceptions.MicrosoftHealthException
    {
        protected ControlPlaneException(string message)
            : base(message)
        {
        }
    }
}
