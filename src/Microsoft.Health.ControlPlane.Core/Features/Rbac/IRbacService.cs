﻿// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Health.ControlPlane.Core.Features.Persistence;

namespace Microsoft.Health.ControlPlane.Core.Features.Rbac
{
    public interface IRbacService
    {
        Task<IdentityProvider> GetIdentityProviderAsync(string name, CancellationToken cancellationToken);

        Task<IEnumerable<IdentityProvider>> GetAllIdentityProvidersAsync(CancellationToken cancellationToken);

        Task<UpsertResponse<IdentityProvider>> UpsertIdentityProviderAsync(IdentityProvider identityProvider, string eTag, CancellationToken cancellationToken);

        Task DeleteIdentityProviderAsync(string name, string eTag, CancellationToken cancellationToken);
    }
}
