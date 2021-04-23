﻿// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System.Net;
using System.Threading;
using System.Threading.Tasks;
using EnsureThat;
using MediatR;
using Microsoft.Health.Core.Features.Security.Authorization;
using Microsoft.Health.Fhir.Core.Exceptions;
using Microsoft.Health.Fhir.Core.Features.Security;
using Microsoft.Health.Fhir.Core.Messages.BulkImport;
using Microsoft.Health.Fhir.TaskManagement;
using TaskStatus = Microsoft.Health.Fhir.TaskManagement.TaskStatus;

namespace Microsoft.Health.Fhir.Core.Features.Operations.BulkImport
{
    public class GetBulkImportRequestHandler : IRequestHandler<GetBulkImportRequest, GetBulkImportResponse>
    {
        private readonly ITaskManager _taskManager;
        private readonly IAuthorizationService<DataActions> _authorizationService;

        public GetBulkImportRequestHandler(ITaskManager taskManager, IAuthorizationService<DataActions> authorizationService)
        {
            EnsureArg.IsNotNull(taskManager, nameof(taskManager));
            EnsureArg.IsNotNull(authorizationService, nameof(authorizationService));

            _taskManager = taskManager;
            _authorizationService = authorizationService;
        }

        public async Task<GetBulkImportResponse> Handle(GetBulkImportRequest request, CancellationToken cancellationToken)
        {
            EnsureArg.IsNotNull(request, nameof(request));

            if (await _authorizationService.CheckAccess(DataActions.Import, cancellationToken) != DataActions.Import)
            {
                throw new UnauthorizedFhirActionException();
            }

            GetBulkImportResponse bulkImportResponse;

            var taskInfo = await _taskManager.GetTaskAsync(request.TaskId, cancellationToken);

            // We have an existing job. We will determine the response based on the status of the bulk import operation.
            if (taskInfo.Status == TaskStatus.Completed)
            {
                bulkImportResponse = new GetBulkImportResponse(HttpStatusCode.OK, taskInfo.Result);
            }
            else if (taskInfo.IsCanceled)
            {
                string failureReason = $"Bulk import {taskInfo.TaskId} was canceled";
                HttpStatusCode failureStatusCode = HttpStatusCode.BadRequest;

                throw new OperationFailedException(
                    string.Format(Resources.OperationFailed, OperationsConstants.BulkImport, failureReason), failureStatusCode);
            }
            else
            {
                bulkImportResponse = new GetBulkImportResponse(HttpStatusCode.Accepted);
            }

            return bulkImportResponse;
        }
    }
}