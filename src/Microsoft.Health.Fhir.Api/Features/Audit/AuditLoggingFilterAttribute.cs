﻿// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System;
using System.Diagnostics;
using System.Net;
using EnsureThat;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Health.Fhir.Api.Features.ActionResults;

namespace Microsoft.Health.Fhir.Api.Features.Audit
{
    [AttributeUsage(AttributeTargets.Class)]
    internal class AuditLoggingFilterAttribute : ActionFilterAttribute
    {
        private readonly IAuditHelper _auditHelper;

        public AuditLoggingFilterAttribute(IAuditHelper auditHelper)
        {
            EnsureArg.IsNotNull(auditHelper, nameof(auditHelper));

            _auditHelper = auditHelper;
        }

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            EnsureArg.IsNotNull(context, nameof(context));

            var actionDescriptor = context.ActionDescriptor as ControllerActionDescriptor;

            Debug.Assert(actionDescriptor != null, "The ActionDescriptor must be ControllerActionDescriptor.");

            if (actionDescriptor != null)
            {
                _auditHelper.LogExecuting(actionDescriptor.ControllerName, actionDescriptor.ActionName);
            }

            base.OnActionExecuting(context);
        }

        public override void OnResultExecuted(ResultExecutedContext context)
        {
            EnsureArg.IsNotNull(context, nameof(context));

            var actionDescriptor = context.ActionDescriptor as ControllerActionDescriptor;

            Debug.Assert(actionDescriptor != null, "The ActionDescriptor must be ControllerActionDescriptor.");

            // The result can either be a FhirResult or an OperationOutcomeResult which both extend BaseActionResult.

            string typeName = null;

            switch (context.Result)
            {
                case FhirResult fhirResult:
                    typeName = fhirResult?.Result?.TypeName;
                    break;
                case OperationOutcomeResult operationOutcomeResult:
                    typeName = operationOutcomeResult?.Result?.TypeName;
                    break;
            }

            _auditHelper.LogExecuted(
                actionDescriptor.ControllerName,
                actionDescriptor.ActionName,
                (HttpStatusCode)context.HttpContext.Response.StatusCode,
                typeName);

            base.OnResultExecuted(context);
        }
    }
}
