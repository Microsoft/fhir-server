﻿// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using EnsureThat;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Health.Extensions.DependencyInjection;
using Microsoft.Health.Fhir.Core;
using Microsoft.Health.Fhir.Core.Features.Validation;

namespace Microsoft.Health.Fhir.Api.Modules
{
    /// <summary>
    /// Adds validators to container
    /// </summary>
    public class ValidationModule : IStartupModule
    {
        /// <inheritdoc />
        public void Load(IServiceCollection services)
        {
            EnsureArg.IsNotNull(services, nameof(services));

            // Adds basic FHIR model validation into MVC
            services.PostConfigure<MvcOptions>(options =>
            {
                // Removes default DataAnnotationsModelValidator
                options.ModelValidatorProviders.Clear();
            });

            services.TypesInSameAssembly(typeof(ResourceNotValidException).Assembly, typeof(Stu3ModelFactory).Assembly)
                .AssignableTo<IValidator>()
                .Singleton()
                .AsSelf()
                .AsImplementedInterfaces();
        }
    }
}
