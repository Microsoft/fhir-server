﻿// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System.Collections.Generic;
using System.Linq;
using EnsureThat;
using FluentValidation;
using FluentValidation.Results;
using FluentValidation.Validators;
using Hl7.Fhir.ElementModel;
using Hl7.FhirPath;
using Microsoft.Health.Fhir.Core.Models;

namespace Microsoft.Health.Fhir.Core.Features.Validation.Narratives
{
    public class NarrativeValidator<T> : NoopPropertyValidator<T, T>
    {
        private readonly INarrativeHtmlSanitizer _narrativeHtmlSanitizer;

        public NarrativeValidator(INarrativeHtmlSanitizer narrativeHtmlSanitizer)
        {
            EnsureArg.IsNotNull(narrativeHtmlSanitizer, nameof(narrativeHtmlSanitizer));

            _narrativeHtmlSanitizer = narrativeHtmlSanitizer;
        }

        public override string Name => nameof(NarrativeValidator<T>);

        public override bool IsValid(ValidationContext<T> context, T value)
        {
            var result = Validate(context);
            List<ValidationFailure> validationFailures = result as List<ValidationFailure> ?? result.ToList();
            foreach (var validationFailure in validationFailures)
            {
                context.AddFailure(validationFailure);
            }

            return validationFailures.Count == 0;
        }

        public IEnumerable<ValidationFailure> Validate(ValidationContext<T> context)
        {
            EnsureArg.IsNotNull(context, nameof(context));

            if (context.InstanceToValidate is ResourceElement resourceElement)
            {
                if (resourceElement.IsDomainResource)
                {
                    foreach (ValidationFailure validationFailure in ValidateResource(resourceElement.Instance))
                    {
                        yield return validationFailure;
                    }
                }
                else if (resourceElement.InstanceType.Equals(KnownResourceTypes.Bundle, System.StringComparison.OrdinalIgnoreCase))
                {
                    var bundleEntries = resourceElement.Instance.Select(KnownFhirPaths.BundleEntries);
                    if (bundleEntries != null)
                    {
                        foreach (ValidationFailure validationFailure in bundleEntries.SelectMany(ValidateResource))
                        {
                            yield return validationFailure;
                        }
                    }
                }
            }
        }

        private IEnumerable<ValidationFailure> ValidateResource(ITypedElement typedElement)
        {
            EnsureArg.IsNotNull(typedElement, nameof(typedElement));

            var xhtml = typedElement.Scalar(KnownFhirPaths.ResourceNarrative) as string;
            if (string.IsNullOrEmpty(xhtml))
            {
                yield break;
            }

            var errors = _narrativeHtmlSanitizer.Validate(xhtml);
            var fullFhirPath = typedElement.InstanceType + "." + KnownFhirPaths.ResourceNarrative;

            foreach (var error in errors)
            {
                yield return new FhirValidationFailure(
                    fullFhirPath,
                    error,
                    new OperationOutcomeIssue(
                        OperationOutcomeConstants.IssueSeverity.Error,
                        OperationOutcomeConstants.IssueType.Structure,
                        error,
                        location: new[] { fullFhirPath }));
            }
        }
    }
}
