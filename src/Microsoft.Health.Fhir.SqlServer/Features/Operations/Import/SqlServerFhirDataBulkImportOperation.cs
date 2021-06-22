﻿// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EnsureThat;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Health.Fhir.Core.Configs;
using Microsoft.Health.Fhir.Core.Features.Operations;
using Microsoft.Health.Fhir.SqlServer.Features.Schema.Model;
using Microsoft.Health.SqlServer.Features.Client;
using Microsoft.Health.SqlServer.Features.Schema.Model;
using Index = Microsoft.Health.SqlServer.Features.Schema.Model.Index;

namespace Microsoft.Health.Fhir.SqlServer.Features.Storage
{
    public class SqlServerFhirDataBulkImportOperation : IFhirDataBulkImportOperation
    {
        private SqlConnectionWrapperFactory _sqlConnectionWrapperFactory;
        private ISqlServerTransientFaultRetryPolicyFactory _sqlServerTransientFaultRetryPolicyFactory;
        private SqlServerFhirModel _model;
        private readonly ImportTaskConfiguration _importTaskConfiguration;
        private ILogger<SqlServerFhirDataBulkImportOperation> _logger;

        public SqlServerFhirDataBulkImportOperation(
            SqlConnectionWrapperFactory sqlConnectionWrapperFactory,
            ISqlServerTransientFaultRetryPolicyFactory sqlServerTransientFaultRetryPolicyFactory,
            SqlServerFhirModel model,
            IOptions<OperationsConfiguration> operationsConfig,
            ILogger<SqlServerFhirDataBulkImportOperation> logger)
        {
            EnsureArg.IsNotNull(sqlConnectionWrapperFactory, nameof(sqlConnectionWrapperFactory));
            EnsureArg.IsNotNull(sqlServerTransientFaultRetryPolicyFactory, nameof(sqlServerTransientFaultRetryPolicyFactory));
            EnsureArg.IsNotNull(model, nameof(model));
            EnsureArg.IsNotNull(operationsConfig, nameof(operationsConfig));
            EnsureArg.IsNotNull(logger, nameof(logger));

            _sqlConnectionWrapperFactory = sqlConnectionWrapperFactory;
            _sqlServerTransientFaultRetryPolicyFactory = sqlServerTransientFaultRetryPolicyFactory;
            _model = model;
            _importTaskConfiguration = operationsConfig.Value.Import;
            _logger = logger;
        }

        public static IReadOnlyList<(Table table, Index index)> UnclusteredIndexes { get; } =
            new List<(Table table, Index index)>()
            {
                (VLatest.Resource, VLatest.Resource.IX_Resource_ResourceSurrogateId),
                (VLatest.Resource, VLatest.Resource.IX_Resource_ResourceTypeId_ResourceId),
                (VLatest.Resource, VLatest.Resource.IX_Resource_ResourceTypeId_ResourceId_Version),
                (VLatest.Resource, VLatest.Resource.IX_Resource_ResourceTypeId_ResourceSurrgateId),
                (VLatest.CompartmentAssignment, VLatest.CompartmentAssignment.IX_CompartmentAssignment_CompartmentTypeId_ReferenceResourceId),
                (VLatest.DateTimeSearchParam, VLatest.DateTimeSearchParam.IX_DateTimeSearchParam_SearchParamId_EndDateTime_StartDateTime),
                (VLatest.DateTimeSearchParam, VLatest.DateTimeSearchParam.IX_DateTimeSearchParam_SearchParamId_EndDateTime_StartDateTime_Long),
                (VLatest.DateTimeSearchParam, VLatest.DateTimeSearchParam.IX_DateTimeSearchParam_SearchParamId_StartDateTime_EndDateTime),
                (VLatest.DateTimeSearchParam, VLatest.DateTimeSearchParam.IX_DateTimeSearchParam_SearchParamId_StartDateTime_EndDateTime_Long),
                (VLatest.NumberSearchParam, VLatest.NumberSearchParam.IX_NumberSearchParam_SearchParamId_HighValue_LowValue),
                (VLatest.NumberSearchParam, VLatest.NumberSearchParam.IX_NumberSearchParam_SearchParamId_LowValue_HighValue),
                (VLatest.NumberSearchParam, VLatest.NumberSearchParam.IX_NumberSearchParam_SearchParamId_SingleValue),
                (VLatest.QuantitySearchParam, VLatest.QuantitySearchParam.IX_QuantitySearchParam_SearchParamId_QuantityCodeId_HighValue_LowValue),
                (VLatest.QuantitySearchParam, VLatest.QuantitySearchParam.IX_QuantitySearchParam_SearchParamId_QuantityCodeId_LowValue_HighValue),
                (VLatest.QuantitySearchParam, VLatest.QuantitySearchParam.IX_QuantitySearchParam_SearchParamId_QuantityCodeId_SingleValue),
                (VLatest.ReferenceSearchParam, VLatest.ReferenceSearchParam.IX_ReferenceSearchParam_SearchParamId_ReferenceResourceTypeId_ReferenceResourceId_BaseUri_ReferenceResourceVersion),
                (VLatest.ReferenceTokenCompositeSearchParam, VLatest.ReferenceTokenCompositeSearchParam.IX_ReferenceTokenCompositeSearchParam_ReferenceResourceId1_Code2),
                (VLatest.StringSearchParam, VLatest.StringSearchParam.IX_StringSearchParam_SearchParamId_Text),
                (VLatest.StringSearchParam, VLatest.StringSearchParam.IX_StringSearchParam_SearchParamId_TextWithOverflow),
                (VLatest.TokenDateTimeCompositeSearchParam, VLatest.TokenDateTimeCompositeSearchParam.IX_TokenDateTimeCompositeSearchParam_Code1_EndDateTime2_StartDateTime2),
                (VLatest.TokenDateTimeCompositeSearchParam, VLatest.TokenDateTimeCompositeSearchParam.IX_TokenDateTimeCompositeSearchParam_Code1_EndDateTime2_StartDateTime2_Long),
                (VLatest.TokenDateTimeCompositeSearchParam, VLatest.TokenDateTimeCompositeSearchParam.IX_TokenDateTimeCompositeSearchParam_Code1_StartDateTime2_EndDateTime2),
                (VLatest.TokenDateTimeCompositeSearchParam, VLatest.TokenDateTimeCompositeSearchParam.IX_TokenDateTimeCompositeSearchParam_Code1_StartDateTime2_EndDateTime2_Long),
                (VLatest.TokenNumberNumberCompositeSearchParam, VLatest.TokenNumberNumberCompositeSearchParam.IX_TokenNumberNumberCompositeSearchParam_SearchParamId_Code1_LowValue2_HighValue2_LowValue3_HighValue3),
                (VLatest.TokenNumberNumberCompositeSearchParam, VLatest.TokenNumberNumberCompositeSearchParam.IX_TokenNumberNumberCompositeSearchParam_SearchParamId_Code1_Text2),
                (VLatest.TokenQuantityCompositeSearchParam, VLatest.TokenQuantityCompositeSearchParam.IX_TokenQuantityCompositeSearchParam_SearchParamId_Code1_QuantityCodeId2_HighValue2_LowValue2),
                (VLatest.TokenQuantityCompositeSearchParam, VLatest.TokenQuantityCompositeSearchParam.IX_TokenQuantityCompositeSearchParam_SearchParamId_Code1_QuantityCodeId2_LowValue2_HighValue2),
                (VLatest.TokenQuantityCompositeSearchParam, VLatest.TokenQuantityCompositeSearchParam.IX_TokenQuantityCompositeSearchParam_SearchParamId_Code1_QuantityCodeId2_SingleValue2),
                (VLatest.TokenSearchParam, VLatest.TokenSearchParam.IX_TokenSeachParam_SearchParamId_Code_SystemId),
                (VLatest.TokenStringCompositeSearchParam, VLatest.TokenStringCompositeSearchParam.IX_TokenStringCompositeSearchParam_SearchParamId_Code1_Text2),
                (VLatest.TokenStringCompositeSearchParam, VLatest.TokenStringCompositeSearchParam.IX_TokenStringCompositeSearchParam_SearchParamId_Code1_Text2WithOverflow),
                (VLatest.TokenText, VLatest.TokenText.IX_TokenText_SearchParamId_Text),
                (VLatest.TokenTokenCompositeSearchParam, VLatest.TokenTokenCompositeSearchParam.IX_TokenTokenCompositeSearchParam_Code1_Code2),
                (VLatest.UriSearchParam, VLatest.UriSearchParam.IX_UriSearchParam_SearchParamId_Uri),

                // ResourceWriteClaim Table - No unclustered index
            };

        public static string ResourceWriteClaimTableName { get; } = VLatest.ResourceWriteClaim.TableName;

        public static IReadOnlyList<string> SearchParameterTables { get; } =
            new List<string>()
            {
                VLatest.CompartmentAssignment.TableName,
                VLatest.ReferenceSearchParam.TableName,
                VLatest.TokenSearchParam.TableName,
                VLatest.TokenText.TableName,
                VLatest.StringSearchParam.TableName,
                VLatest.UriSearchParam.TableName,
                VLatest.NumberSearchParam.TableName,
                VLatest.QuantitySearchParam.TableName,
                VLatest.DateTimeSearchParam.TableName,
                VLatest.ReferenceTokenCompositeSearchParam.TableName,
                VLatest.TokenTokenCompositeSearchParam.TableName,
                VLatest.TokenDateTimeCompositeSearchParam.TableName,
                VLatest.TokenQuantityCompositeSearchParam.TableName,
                VLatest.TokenStringCompositeSearchParam.TableName,
                VLatest.TokenNumberNumberCompositeSearchParam.TableName,
            };

        public async Task BulkCopyDataAsync(DataTable dataTable, CancellationToken cancellationToken)
        {
            using (SqlConnectionWrapper sqlConnectionWrapper = await _sqlConnectionWrapperFactory.ObtainSqlConnectionWrapperAsync(cancellationToken, true))
            using (SqlBulkCopy bulkCopy = new SqlBulkCopy(sqlConnectionWrapper.SqlConnection, SqlBulkCopyOptions.CheckConstraints | SqlBulkCopyOptions.UseInternalTransaction | SqlBulkCopyOptions.KeepNulls, null))
            {
                bulkCopy.DestinationTableName = dataTable.TableName;

                try
                {
                    await _sqlServerTransientFaultRetryPolicyFactory.Create().ExecuteAsync(
                        async () =>
                        {
                            bulkCopy.BulkCopyTimeout = _importTaskConfiguration.SqlBulkOperationTimeoutInSec;
                            await bulkCopy.WriteToServerAsync(dataTable.CreateDataReader());
                        });
                }
                catch (Exception ex)
                {
                    _logger.LogInformation(ex, "Failed to bulk copy data.");

                    throw;
                }
            }
        }

        public async Task CleanBatchResourceAsync(string resourceType, long beginSequenceId, long endSequenceId, CancellationToken cancellationToken)
        {
            short resourceTypeId = _model.GetResourceTypeId(resourceType);

            await BatchDeleteResourcesInternalAsync(beginSequenceId, endSequenceId, resourceTypeId, _importTaskConfiguration.SqlCleanResourceBatchSize, cancellationToken);
            await BatchDeleteResourceWriteClaimsInternalAsync(beginSequenceId, endSequenceId, _importTaskConfiguration.SqlCleanResourceBatchSize, cancellationToken);

            foreach (var tableName in SearchParameterTables.ToArray())
            {
                await BatchDeleteResourceParamsInternalAsync(tableName, beginSequenceId, endSequenceId, resourceTypeId, _importTaskConfiguration.SqlCleanResourceBatchSize, cancellationToken);
            }
        }

        public async Task PreprocessAsync(CancellationToken cancellationToken)
        {
            using (SqlConnectionWrapper sqlConnectionWrapper = await _sqlConnectionWrapperFactory.ObtainSqlConnectionWrapperAsync(cancellationToken, true))
            using (SqlCommandWrapper sqlCommandWrapper = sqlConnectionWrapper.CreateSqlCommand())
            {
                try
                {
                    IndexTableTypeV1Row[] indexes = UnclusteredIndexes.Select(indexRecord => new IndexTableTypeV1Row(indexRecord.table.TableName, indexRecord.index.IndexName)).ToArray();

                    VLatest.DisableIndexes.PopulateCommand(sqlCommandWrapper, indexes);
                    await sqlCommandWrapper.ExecuteNonQueryAsync(cancellationToken);
                }
                catch (SqlException sqlEx)
                {
                    _logger.LogInformation(sqlEx, "Failed to disable indexes.");

                    throw;
                }
            }
        }

        public async Task PostprocessAsync(CancellationToken cancellationToken)
        {
            (string tableName, string indexName)[] allIndexTuples = UnclusteredIndexes.Select(indexRecord => (indexRecord.table.TableName, indexRecord.index.IndexName)).ToArray();
            (string tableName, string indexName)[] disabledndexTuples = (await GetIndexesDisableStatusAsync(allIndexTuples, cancellationToken)).ToArray();
            IndexTableTypeV1Row[] disabledIndexes = disabledndexTuples.Select(disabledndexTuple => new IndexTableTypeV1Row(disabledndexTuple.tableName, disabledndexTuple.indexName)).ToArray();
            List<Task> runningTasks = new List<Task>();

            foreach (IndexTableTypeV1Row index in disabledIndexes)
            {
                while (runningTasks.Count >= _importTaskConfiguration.SqlMaxRebuildIndexOperationConcurrentCount)
                {
                    Task completedTask = await Task.WhenAny(runningTasks.ToArray());
                    await completedTask;

                    runningTasks.Remove(completedTask);
                }

                runningTasks.Add(ExecuteRebuildIndexTaskAsync(index, cancellationToken));
            }

            await Task.WhenAll(runningTasks.ToArray());
        }

        public async Task DeleteDuplicatedResourcesAsync(CancellationToken cancellationToken)
        {
            try
            {
                await ExecuteDeleteDuplicatedResourcesTaskAsync(cancellationToken);

                List<Task> runningTasks = new List<Task>();

                runningTasks.Add(ExecuteDeleteDuplicatedSearchParamsTaskAsync(ResourceWriteClaimTableName, cancellationToken));
                foreach (var tableName in SearchParameterTables.ToArray())
                {
                    if (runningTasks.Count >= _importTaskConfiguration.SqlMaxDeleteDuplicateOperationConcurrentCount)
                    {
                        Task completedTask = await Task.WhenAny(runningTasks);
                        runningTasks.Remove(completedTask);
                        await completedTask;
                    }

                    runningTasks.Add(ExecuteDeleteDuplicatedSearchParamsTaskAsync(tableName, cancellationToken));
                }

                while (runningTasks.Count > 0)
                {
                    await runningTasks.First();
                    runningTasks.RemoveAt(0);
                }
            }
            catch (SqlException sqlEx)
            {
                _logger.LogInformation(sqlEx, "Failed to delete duplicate resources.");

                throw;
            }
        }

        public async Task<IReadOnlyList<(string tableName, string indexName)>> GetDisabledIndexes((string tableName, string indexName)[] indexTuples, CancellationToken cancellationToken)
        {
            using (SqlConnectionWrapper sqlConnectionWrapper = await _sqlConnectionWrapperFactory.ObtainSqlConnectionWrapperAsync(cancellationToken, true))
            using (SqlCommandWrapper sqlCommandWrapper = sqlConnectionWrapper.CreateSqlCommand())
            {
                try
                {
                    IndexTableTypeV1Row[] indexes = indexTuples.Select(indexTuple => new IndexTableTypeV1Row(indexTuple.tableName, indexTuple.indexName)).ToArray();
                    sqlCommandWrapper.CommandTimeout = _importTaskConfiguration.SqlBulkOperationTimeoutInSec;

                    VLatest.GetDisabledIndexess.PopulateCommand(sqlCommandWrapper, indexes);
                    using (SqlDataReader sqlDataReader = await sqlCommandWrapper.ExecuteReaderAsync(CommandBehavior.SequentialAccess, cancellationToken))
                    {
                        var disabledIndexTuples = new List<(string tableName, string indexName)>();
                        while (await sqlDataReader.ReadAsync(cancellationToken))
                        {
                            disabledIndexTuples.Add((sqlDataReader.GetString(0), sqlDataReader.GetString(1)));
                        }

                        return disabledIndexTuples;
                    }
                }
                catch (SqlException sqlEx)
                {
                    _logger.LogInformation(sqlEx, "Failed get disabled indexes.");

                    throw;
                }
            }
        }

        private async Task ExecuteRebuildIndexTaskAsync(IndexTableTypeV1Row index, CancellationToken cancellationToken)
        {
            using (SqlConnectionWrapper sqlConnectionWrapper = await _sqlConnectionWrapperFactory.ObtainSqlConnectionWrapperAsync(cancellationToken, true))
            using (SqlCommandWrapper sqlCommandWrapper = sqlConnectionWrapper.CreateSqlCommand())
            {
                try
                {
                    sqlCommandWrapper.CommandTimeout = _importTaskConfiguration.SqlLongRunningOperationTimeoutInSec;

                    VLatest.RebuildIndexes.PopulateCommand(sqlCommandWrapper, new IndexTableTypeV1Row[] { index });
                    await sqlCommandWrapper.ExecuteNonQueryAsync(cancellationToken);
                }
                catch (SqlException sqlEx)
                {
                    _logger.LogInformation(sqlEx, "Failed to rebuild indexes.");

                    throw;
                }
            }
        }

        private async Task ExecuteDeleteDuplicatedResourcesTaskAsync(CancellationToken cancellationToken)
        {
            using (SqlConnectionWrapper sqlConnectionWrapper = await _sqlConnectionWrapperFactory.ObtainSqlConnectionWrapperAsync(cancellationToken, true))
            using (SqlCommandWrapper sqlCommandWrapper = sqlConnectionWrapper.CreateSqlCommand())
            {
                try
                {
                    sqlCommandWrapper.CommandTimeout = _importTaskConfiguration.SqlLongRunningOperationTimeoutInSec;

                    VLatest.DeleteDuplicatedResources.PopulateCommand(sqlCommandWrapper);
                    await sqlCommandWrapper.ExecuteNonQueryAsync(cancellationToken);
                }
                catch (SqlException sqlEx)
                {
                    _logger.LogInformation(sqlEx, "Failed to delete resoueces duplicate search paramters.");

                    throw;
                }
            }
        }

        private async Task ExecuteDeleteDuplicatedSearchParamsTaskAsync(string tableName, CancellationToken cancellationToken)
        {
            using (SqlConnectionWrapper sqlConnectionWrapper = await _sqlConnectionWrapperFactory.ObtainSqlConnectionWrapperAsync(cancellationToken, true))
            using (SqlCommandWrapper sqlCommandWrapper = sqlConnectionWrapper.CreateSqlCommand())
            {
                try
                {
                    sqlCommandWrapper.CommandTimeout = _importTaskConfiguration.SqlLongRunningOperationTimeoutInSec;

                    VLatest.DeleteDuplicatedSearchParams.PopulateCommand(sqlCommandWrapper, tableName);
                    await sqlCommandWrapper.ExecuteNonQueryAsync(cancellationToken);
                }
                catch (SqlException sqlEx)
                {
                    _logger.LogInformation(sqlEx, "Failed to delete duplicate search paramters.");

                    throw;
                }
            }
        }

        private async Task BatchDeleteResourcesInternalAsync(long beginSequenceId, long endSequenceId, short resourceTypeId, int batchSize, CancellationToken cancellationToken)
        {
            while (true)
            {
                using (SqlConnectionWrapper sqlConnectionWrapper = await _sqlConnectionWrapperFactory.ObtainSqlConnectionWrapperAsync(cancellationToken, true))
                using (SqlCommandWrapper sqlCommandWrapper = sqlConnectionWrapper.CreateSqlCommand())
                {
                    try
                    {
                        sqlCommandWrapper.CommandTimeout = _importTaskConfiguration.SqlBulkOperationTimeoutInSec;

                        VLatest.BatchDeleteResources.PopulateCommand(sqlCommandWrapper, resourceTypeId, beginSequenceId, endSequenceId, batchSize);
                        int impactRows = await sqlCommandWrapper.ExecuteNonQueryAsync(cancellationToken);

                        if (impactRows < batchSize)
                        {
                            return;
                        }
                    }
                    catch (SqlException sqlEx)
                    {
                        _logger.LogInformation(sqlEx, "Failed batch delete Resource.");

                        throw;
                    }
                }
            }
        }

        private async Task BatchDeleteResourceWriteClaimsInternalAsync(long beginSequenceId, long endSequenceId, int batchSize, CancellationToken cancellationToken)
        {
            while (true)
            {
                using (SqlConnectionWrapper sqlConnectionWrapper = await _sqlConnectionWrapperFactory.ObtainSqlConnectionWrapperAsync(cancellationToken, true))
                using (SqlCommandWrapper sqlCommandWrapper = sqlConnectionWrapper.CreateSqlCommand())
                {
                    try
                    {
                        sqlCommandWrapper.CommandTimeout = _importTaskConfiguration.SqlBulkOperationTimeoutInSec;

                        VLatest.BatchDeleteResourceWriteClaims.PopulateCommand(sqlCommandWrapper, beginSequenceId, endSequenceId, batchSize);
                        int impactRows = await sqlCommandWrapper.ExecuteNonQueryAsync(cancellationToken);

                        if (impactRows < batchSize)
                        {
                            return;
                        }
                    }
                    catch (SqlException sqlEx)
                    {
                        _logger.LogInformation(sqlEx, "Failed batch delete ResourceWriteClaims.");

                        throw;
                    }
                }
            }
        }

        private async Task BatchDeleteResourceParamsInternalAsync(string tableName, long beginSequenceId, long endSequenceId, short resourceTypeId, int batchSize, CancellationToken cancellationToken)
        {
            while (true)
            {
                using (SqlConnectionWrapper sqlConnectionWrapper = await _sqlConnectionWrapperFactory.ObtainSqlConnectionWrapperAsync(cancellationToken, true))
                using (SqlCommandWrapper sqlCommandWrapper = sqlConnectionWrapper.CreateSqlCommand())
                {
                    try
                    {
                        sqlCommandWrapper.CommandTimeout = _importTaskConfiguration.SqlBulkOperationTimeoutInSec;

                        VLatest.BatchDeleteResourceParams.PopulateCommand(sqlCommandWrapper, tableName, resourceTypeId, beginSequenceId, endSequenceId, batchSize);
                        int impactRows = await sqlCommandWrapper.ExecuteNonQueryAsync(cancellationToken);

                        if (impactRows < batchSize)
                        {
                            return;
                        }
                    }
                    catch (SqlException sqlEx)
                    {
                        _logger.LogInformation(sqlEx, "Failed batch delete ResourceParams.");

                        throw;
                    }
                }
            }
        }
    }
}
