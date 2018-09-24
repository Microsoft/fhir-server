﻿// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System;
using System.Security.Cryptography;
using System.Text;
using EnsureThat;
using Microsoft.Health.Fhir.Core.Extensions;
using Newtonsoft.Json;

namespace Microsoft.Health.Fhir.CosmosDb.Features.Storage.Continuation
{
    public class ContinuationToken : SystemData
    {
        internal const string ContinuationTokenPartition = "_continuationTokens";

        public ContinuationToken(string continuationToken)
        {
            EnsureArg.IsNotNullOrWhiteSpace(continuationToken, nameof(continuationToken));

            Id = Hash(continuationToken);
            Token = continuationToken;
            TimeToLive = (int)TimeSpan.FromDays(1).TotalSeconds;
        }

        [JsonConstructor]
        protected ContinuationToken()
        {
        }

        [JsonProperty("continuationToken")]
        public string Token { get; protected set; }

        [JsonProperty("ttl", NullValueHandling = NullValueHandling.Ignore)]
        public int? TimeToLive { get; protected set; }

        [JsonProperty("partitionKey")]
        public string PartitionKey { get; } = ContinuationTokenPartition;

        private static string Hash(string token)
        {
            using (var hasher = new SHA256Managed())
            {
                return hasher.ComputeHash(Encoding.UTF8.GetBytes(token)).ToSafeBase64();
            }
        }
    }
}
