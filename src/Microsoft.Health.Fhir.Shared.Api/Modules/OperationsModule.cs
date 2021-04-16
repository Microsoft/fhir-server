﻿// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using EnsureThat;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Health.Extensions.DependencyInjection;
using Microsoft.Health.Fhir.Api.Features.Operations;
using Microsoft.Health.Fhir.Core.Features.Conformance;
using Microsoft.Health.Fhir.Core.Features.Operations.Export;
using Microsoft.Health.Fhir.Core.Features.Operations.Import;
using Microsoft.Health.Fhir.Core.Features.Operations.Reindex;

namespace Microsoft.Health.Fhir.Api.Modules
{
    /// <summary>
    /// Registration of operations components.
    /// </summary>
    public class OperationsModule : IStartupModule
    {
        public void Load(IServiceCollection services)
        {
            EnsureArg.IsNotNull(services, nameof(services));

            services.Add<GroupMemberExtractor>()
                .Singleton()
                .AsService<IGroupMemberExtractor>();

            services.Add<ExportJobTask>()
                .Transient()
                .AsSelf();

            services.Add<IExportJobTask>(sp => sp.GetRequiredService<ExportJobTask>())
                .Transient()
                .AsSelf()
                .AsFactory();

            services.Add<ExportJobWorker>()
                .Singleton()
                .AsSelf();

            services.Add<ResourceToNdjsonBytesSerializer>()
                .Singleton()
                .AsService<IResourceToByteArraySerializer>();

            services.Add<ReindexJobTask>()
                .Transient()
                .AsSelf();

            services.Add<IReindexJobTask>(sp => sp.GetRequiredService<ReindexJobTask>())
                .Transient()
                .AsSelf()
                .AsFactory();

            services.Add<ReindexJobWorker>()
                .Singleton()
                .AsSelf();

            services.AddSingleton<IReindexUtilities, ReindexUtilities>();

            services.Add<OperationsCapabilityProvider>()
                .Transient()
                .AsService<IProvideCapability>();

            services.Add<BulkResourceLoader>()
                .Transient()
                .AsSelf()
                .AsImplementedInterfaces();

            services.Add<BulkRawResourceProcessor>()
                .Transient()
                .AsImplementedInterfaces()
                .AsSelf();

            services.Add<BulkImportDataExtractor>()
                .Transient()
                .AsSelf()
                .AsImplementedInterfaces();
        }
    }
}
