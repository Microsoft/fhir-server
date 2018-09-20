﻿// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using EnsureThat;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Health.Extensions.DependencyInjection;
using Microsoft.Health.Fhir.Api.Features.Binders;
using Microsoft.Health.Fhir.Api.Features.Filters;
using Microsoft.Health.Fhir.Api.Features.Routing;

namespace Microsoft.Health.Fhir.Api.Modules
{
    public class MvcModule : IStartupModule
    {
        private readonly EmbeddedFileProvider _embeddedFileProvider;

        public MvcModule()
        {
            _embeddedFileProvider = new EmbeddedFileProvider(GetType().Assembly);
        }

        /// <inheritdoc />
        public void Load(IServiceCollection services)
        {
            EnsureArg.IsNotNull(services, nameof(services));

            // Adds route constraint for FHIR resource types
            services.Configure<RouteOptions>(options => options.ConstraintMap.Add(KnownRoutes.ResourceTypeRouteConstraint, typeof(ResourceTypesRouteConstraint)));

            // Adds provider to serve embedded razor views
            services.Configure<RazorViewEngineOptions>(options =>
            {
                options.FileProviders.Add(_embeddedFileProvider);
            });

            services.PostConfigure<MvcOptions>(options =>
            {
                options.ModelBinderProviders.Insert(0, new PartialDateTimeBinderProvider());
                options.Filters.Add(typeof(FhirRequestContextRouteNameFilterAttribute));
            });

            // These are needed for IUrlResolver used by search.
            // If we update the search implementation to not use these, we should remove
            // the registration since enabling these accessors has performance implications.
            // https://github.com/aspnet/Hosting/issues/793
            services.AddSingleton<IActionContextAccessor, ActionContextAccessor>();
            services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
        }
    }
}
