﻿// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using EnsureThat;
using Microsoft.Health.Core;
using Microsoft.Health.Fhir.Core.Models;
using Newtonsoft.Json;

namespace Microsoft.Health.Fhir.Core.Features.Operations.Reindex.Models
{
    /// <summary>
    /// Class to hold metadata for an individual reindex job.
    /// </summary>
    public class ReindexJobRecord : JobRecord
    {
        private int queryDelayIntervalInMilliseconds;
        private ushort maximumConcurrency;
        private uint maximumNumberOfResourcesPerQuery;
        private ushort? targetDataStoreUsagePercentage;
        private IReadOnlyCollection<string> targetResourceTypes;

        public ReindexJobRecord(
            IReadOnlyDictionary<string, string> searchParametersHash,
            IReadOnlyCollection<string> targetResourceTypes,
            ushort maxiumumConcurrency = 1,
            uint maxResourcesPerQuery = 100,
            int queryDelayIntervalInMilliseconds = 500,
            ushort? targetDataStoreUsagePercentage = null)
        {
            EnsureArg.IsNotNull(searchParametersHash, nameof(searchParametersHash));
            EnsureArg.IsNotNull(targetResourceTypes, nameof(targetResourceTypes));

            // Default values
            SchemaVersion = 1;
            Id = Guid.NewGuid().ToString();
            Status = OperationStatus.Queued;

            QueuedTime = Clock.UtcNow;
            LastModified = Clock.UtcNow;

            ResourceTypeSearchParameterHashMap = searchParametersHash;
            MaximumConcurrency = maxiumumConcurrency;
            MaximumNumberOfResourcesPerQuery = maxResourcesPerQuery;
            QueryDelayIntervalInMilliseconds = queryDelayIntervalInMilliseconds;
            TargetDataStoreUsagePercentage = targetDataStoreUsagePercentage;
            TargetResourceTypes = targetResourceTypes;
        }

        [JsonConstructor]
        protected ReindexJobRecord()
        {
        }

        [JsonProperty(JobRecordProperties.MaximumConcurrency)]
        public ushort MaximumConcurrency
        {
            get
            {
                return maximumConcurrency;
            }

            set
            {
                if (value > 10)
                {
                    throw new ArgumentOutOfRangeException(nameof(MaximumConcurrency));
                }
                else
                {
                    maximumConcurrency = value;
                }
            }
        }

        [JsonProperty(JobRecordProperties.Error)]
        public ICollection<OperationOutcomeIssue> Error { get; private set; } = new List<OperationOutcomeIssue>();

        /// <summary>
        /// Use Concurrent dictionary to allow access to specific items in the list
        /// Ignore the byte value field, effective using the dictionary as a hashset
        /// </summary>
        [JsonProperty(JobRecordProperties.QueryList)]
        [JsonConverter(typeof(ReindexJobQueryStatusConverter))]

        public ConcurrentDictionary<ReindexJobQueryStatus, byte> QueryList { get; private set; } = new ConcurrentDictionary<ReindexJobQueryStatus, byte>();

        [JsonProperty(JobRecordProperties.ResourceCounts)]
        public ConcurrentDictionary<string, int> ResourceCounts { get; private set; } = new ConcurrentDictionary<string, int>();

        [JsonProperty(JobRecordProperties.Count)]
        public int Count { get; set; }

        [JsonProperty(JobRecordProperties.Progress)]
        public int Progress { get; set; }

        [JsonProperty(JobRecordProperties.ResourceTypeSearchParameterHashMap)]
        public IReadOnlyDictionary<string, string> ResourceTypeSearchParameterHashMap { get; private set; }

        [JsonProperty(JobRecordProperties.LastModified)]
        public DateTimeOffset LastModified { get; set; }

        [JsonProperty(JobRecordProperties.FailureCount)]
        public ushort FailureCount { get; set; }

        [JsonProperty(JobRecordProperties.Resources)]
        public ICollection<string> Resources { get; private set; } = new List<string>();

        [JsonProperty(JobRecordProperties.SearchParams)]
        public ICollection<string> SearchParams { get; private set; } = new List<string>();

        [JsonProperty(JobRecordProperties.MaximumNumberOfResourcesPerQuery)]
        public uint MaximumNumberOfResourcesPerQuery
        {
            get
            {
                return maximumNumberOfResourcesPerQuery;
            }

            set
            {
                if (value < 1 || value > 5000)
                {
                    throw new ArgumentOutOfRangeException(nameof(MaximumNumberOfResourcesPerQuery));
                }
                else
                {
                    maximumNumberOfResourcesPerQuery = value;
                }
            }
        }

        /// <summary>
        /// Controls the time between queries of resources to be reindexed
        /// </summary>
        [JsonProperty(JobRecordProperties.QueryDelayIntervalInMilliseconds)]
        public int QueryDelayIntervalInMilliseconds
        {
            get
            {
                return queryDelayIntervalInMilliseconds;
            }

            set
            {
                if (value < 5 || value > 500000)
                {
                    throw new ArgumentOutOfRangeException(nameof(QueryDelayIntervalInMilliseconds));
                }
                else
                {
                    queryDelayIntervalInMilliseconds = value;
                }
            }
        }

        /// <summary>
        /// Controls the target percentage of how much of the allocated
        /// data store resources to use
        /// Ex: 1 - 100 percent of provisioned datastore resources
        /// 0 means the value is not set, no throttling will occur
        /// </summary>
        [JsonProperty(JobRecordProperties.TargetDataStoreUsagePercentage)]
        public ushort? TargetDataStoreUsagePercentage
        {
            get
            {
                return targetDataStoreUsagePercentage;
            }

            set
            {
                if (value < 0 || value > 100)
                {
                    throw new ArgumentOutOfRangeException(nameof(TargetDataStoreUsagePercentage));
                }
                else
                {
                    targetDataStoreUsagePercentage = value;
                }
            }
        }

        /// <summary>
        /// A user can optionally limit the scope of the Reindex job to specific
        /// resource types
        /// </summary>
        [JsonProperty(JobRecordProperties.TargetResourceTypes)]
        public IReadOnlyCollection<string> TargetResourceTypes
        {
            get
            {
                return targetResourceTypes;
            }

            set
            {
                foreach (var type in value)
                {
                    ModelInfoProvider.EnsureValidResourceType(type, nameof(type));
                }

                targetResourceTypes = value;
            }
        }

        [JsonIgnore]
        public string ResourceList
        {
            get { return string.Join(",", Resources); }
        }

        [JsonIgnore]
        public string SearchParamList
        {
            get { return string.Join(",", SearchParams); }
        }

        [JsonIgnore]
        public string TargetResourceTypeList
        {
            get { return string.Join(",", TargetResourceTypes); }
        }
    }
}
