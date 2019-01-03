﻿// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using EnsureThat;

namespace Microsoft.Health.ControlPlane.Core.Features.Exceptions
{
    public class IdentityProviderNotFoundException : ControlPlaneException
    {
        public IdentityProviderNotFoundException(string name)
            : base(ValidateAndFormatMessage(name))
        {
        }

        private static string ValidateAndFormatMessage(string name)
        {
            EnsureArg.IsNotNullOrWhiteSpace(name, nameof(name));

            return string.Format(Resources.IdentityProviderNotFound, name);
        }
    }
}
