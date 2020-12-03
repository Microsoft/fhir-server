﻿// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EnsureThat;
using Hl7.Fhir.Model;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Health.Api.Features.Audit;
using Microsoft.Health.Fhir.Api.Features.Filters;
using Microsoft.Health.Fhir.Api.Features.Routing;
using Microsoft.Health.Fhir.Core.Configs;
using Microsoft.Health.Fhir.Core.Exceptions;
using Microsoft.Health.Fhir.Core.Features.Operations;
using Microsoft.Health.Fhir.Core.Features.Operations.ConvertData.Models;
using Microsoft.Health.Fhir.Core.Messages.ConvertData;
using Microsoft.Health.Fhir.TemplateManagement.Models;
using Microsoft.Health.Fhir.ValueSets;

namespace Microsoft.Health.Fhir.Api.Controllers
{
    [ServiceFilter(typeof(AuditLoggingFilterAttribute))]
    [ServiceFilter(typeof(OperationOutcomeExceptionFilterAttribute))]
    [ServiceFilter(typeof(ValidateContentTypeFilterAttribute))]
    [ValidateResourceTypeFilter]
    [ValidateModelState]
    public class ConvertDataController : Controller
    {
        private readonly IMediator _mediator;
        private readonly ILogger _logger;
        private readonly ConvertDataConfiguration _config;
        private static HashSet<string> _supportedParams = GetSupportedParams();

        private const char ImageRegistryDelimiter = '/';

        public ConvertDataController(
            IMediator mediator,
            IOptions<OperationsConfiguration> operationsConfig,
            ILogger<ConvertDataController> logger)
        {
            EnsureArg.IsNotNull(mediator, nameof(mediator));
            EnsureArg.IsNotNull(operationsConfig?.Value?.ConvertData, nameof(operationsConfig));
            EnsureArg.IsNotNull(logger, nameof(logger));

            _mediator = mediator;
            _config = operationsConfig.Value.ConvertData;
            _logger = logger;
        }

        [HttpPost]
        [Route(KnownRoutes.ConvertData)]
        [AuditEventType(AuditEventSubType.ConvertData)]
        public async Task<IActionResult> ConvertData([FromBody] Parameters inputParams)
        {
            CheckIfConvertDataIsEnabled();

            ValidateParams(inputParams);

            string inputData = ReadStringParameter(inputParams, ConvertDataProperties.InputData);
            string templateCollectionReference = ReadStringParameter(inputParams, ConvertDataProperties.TemplateCollectionReference);
            string rootTemplate = ReadStringParameter(inputParams, ConvertDataProperties.RootTemplate);
            ConversionInputDataType inputDataType = ReadEnumParameter<ConversionInputDataType>(inputParams, ConvertDataProperties.InputDataType);

            // Validate template reference format.
            if (!ImageInfo.IsValidImageReference(templateCollectionReference))
            {
                _logger.LogInformation("Templates collection reference format is invalid.");
                throw new RequestNotValidException(string.Format(Resources.InvalidTemplateCollectionReference, templateCollectionReference));
            }

            // Validate template registry has been configured.
            bool isDefaultTemplateReference = IsDefaultTemplateReference(templateCollectionReference);
            string registryServer = ExtractRegistryServer(templateCollectionReference);
            if (!isDefaultTemplateReference)
            {
                CheckIfRegistryIsConfigured(registryServer);
            }

            var convertDataRequest = new ConvertDataRequest(inputData, inputDataType, registryServer, isDefaultTemplateReference, templateCollectionReference, rootTemplate);
            ConvertDataResponse response = await _mediator.Send(convertDataRequest, cancellationToken: default);

            return new ContentResult
            {
                Content = response.Resource,
                ContentType = "text/plain",
            };
        }

        /// <summary>
        /// Extract the first component from the image reference in the format of "dockerregistry.io/fedora/httpd:version1.0"
        /// Reference format: https://docs.docker.com/engine/reference/commandline/tag/#extended-description
        /// </summary>
        /// <param name="templateCollectionReference">A string of image reference.</param>
        /// <returns>Registry server.</returns>
        private string ExtractRegistryServer(string templateCollectionReference)
        {
            var referenceComponents = templateCollectionReference.Split(ImageRegistryDelimiter);
            if (referenceComponents.Length <= 1 || string.IsNullOrWhiteSpace(referenceComponents.First()))
            {
                _logger.LogInformation("Templates collection reference is invalid: registry server missing.");
                throw new RequestNotValidException(string.Format(Resources.InvalidTemplateCollectionReference, templateCollectionReference));
            }

            return referenceComponents[0];
        }

        private void ValidateParams(Parameters inputParams)
        {
            if (inputParams == null)
            {
                _logger.LogInformation("Failed to deserialize data convert request body as Parameters resource.");
                throw new RequestNotValidException(Resources.ConvertDataParametersNotValid);
            }

            foreach (var param in inputParams.Parameter)
            {
                var paramName = param.Name;
                if (!_supportedParams.Contains(paramName))
                {
                    throw new RequestNotValidException(string.Format(Resources.ConvertDataParameterNotValid, paramName));
                }
            }
        }

        private static string ReadStringParameter(Parameters parameters, string paramName)
        {
            var param = parameters?.Parameter.Find(p =>
                string.Equals(p.Name, paramName, StringComparison.OrdinalIgnoreCase));

            var paramValue = param?.Value?.ToString();
            if (string.IsNullOrEmpty(paramValue))
            {
                throw new RequestNotValidException(string.Format(Resources.ConvertDataParameterValueNotValid, paramName));
            }

            return paramValue;
        }

        private static T ReadEnumParameter<T>(Parameters parameters, string paramName)
        {
            var param = parameters?.Parameter.Find(p =>
                string.Equals(p.Name, paramName, StringComparison.OrdinalIgnoreCase));

            object enumValue;
            if (!Enum.TryParse(typeof(T), param?.Value?.ToString(), ignoreCase: true, out enumValue))
            {
                throw new RequestNotValidException(string.Format(Resources.ConvertDataParameterValueNotValid, paramName));
            }

            return (T)enumValue;
        }

        private static HashSet<string> GetSupportedParams()
        {
            var supportedParams = new HashSet<string>()
            {
                ConvertDataProperties.InputData,
                ConvertDataProperties.InputDataType,
                ConvertDataProperties.TemplateCollectionReference,
                ConvertDataProperties.RootTemplate,
            };

            return supportedParams;
        }

        private static bool IsDefaultTemplateReference(string templateReference)
        {
            return string.Equals(ImageInfo.DefaultTemplateImageReference, templateReference, StringComparison.OrdinalIgnoreCase);
        }

        private void CheckIfRegistryIsConfigured(string registryServer)
        {
            if (!_config.ContainerRegistryServers.Any(server =>
                string.Equals(server, registryServer, StringComparison.OrdinalIgnoreCase)))
            {
                _logger.LogError("The requested ACR server is not configured.");
                throw new ContainerRegistryNotConfiguredException(string.Format(Resources.ContainerRegistryNotConfigured, registryServer));
            }
        }

        private void CheckIfConvertDataIsEnabled()
        {
            if (!_config.Enabled)
            {
                throw new RequestNotValidException(string.Format(Resources.OperationNotEnabled, OperationsConstants.ConvertData));
            }
        }
    }
}
