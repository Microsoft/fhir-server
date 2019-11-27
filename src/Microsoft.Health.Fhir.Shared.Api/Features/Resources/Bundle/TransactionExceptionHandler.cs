﻿// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System.Collections.Generic;
using System.Net;
using Hl7.Fhir.Model;
using Microsoft.Health.Fhir.Core.Features.Persistence;
using Microsoft.Health.Fhir.Core.Models;

namespace Microsoft.Health.Fhir.Api.Features.Resources.Bundle
{
    public static class TransactionExceptionHandler
    {
        public static void ThrowTransactionException(string errorMessage, HttpStatusCode statusCode, OperationOutcome operationOutcome)
        {
            var operationOutcomeIssues = GetOperationOutcomeIssues(operationOutcome.Issue);

            throw new TransactionFailedException(errorMessage, statusCode, operationOutcomeIssues);
        }

        public static List<OperationOutcomeIssue> GetOperationOutcomeIssues(List<OperationOutcome.IssueComponent> operationoutcomeIssueList)
        {
            var issues = new List<OperationOutcomeIssue>();

            operationoutcomeIssueList.ForEach(x =>
                issues.Add(new OperationOutcomeIssue(
                    x.Severity.ToString(),
                    x.Code.ToString(),
                    x.Diagnostics)));

            return issues;
        }
    }
}
