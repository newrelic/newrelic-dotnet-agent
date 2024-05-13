// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using NewRelic.Agent.Core.JsonConverters;
using Newtonsoft.Json;

namespace NewRelic.Agent.Core.WireModels
{
    /// <summary>
    /// Jsonable object containing all of the things necessary to serialize a SQL Trace Data for the sql_trace_data collector command.
    /// </summary>
    /// <remarks>https://pdx-hudson.datanerd.us/job/collector-master/javadoc/com/nr/entities/SqlTrace.html</remarks>
    [JsonConverter(typeof(JsonArrayConverter))]
    public class SqlTraceWireModel : IWireModel
    {
        /// <summary>
        /// ex. WebTransaction/ASP/post.aspx
        /// </summary>
        [JsonArrayIndex(Index = 0)]
        public virtual string TransactionName { get; }

        /// <summary>
        /// ex. http://localhost:8080/post.aspx
        /// </summary>
        [JsonArrayIndex(Index = 1)]
        public virtual string Uri { get; }

        /// <summary>
        /// The hash code of the obfuscated sql string.
        /// ex. 1530282818
        /// </summary>
        [JsonArrayIndex(Index = 2)]
        public virtual long SqlId { get; }

        /// <summary>
        /// The sql string for the slowest statement.
        /// ex. DELETE FROM be_DataStoreSettings WHERE ExtensionType = @type AND ExtensionId = @id; 
        /// </summary>        
        [JsonArrayIndex(Index = 3)]
        public virtual string Sql { get; }

        /// <summary>
        /// The name of the database metric that this sql statement is associated with.
        /// ex. Datastore/statement/MySQL/be_DataStoreSettings/DELETE
        /// </summary>
        [JsonArrayIndex(Index = 4)]
        public virtual string DatastoreMetricName { get; }

        /// <summary>
        /// Total call count ex. 1
        /// </summary>
        [JsonArrayIndex(Index = 5)]
        public virtual uint CallCount { get; }

        /// <summary>
        /// Total call time in milliseconds.
        /// ex. 48.835
        /// </summary>
        [JsonArrayIndex(Index = 6)]
        [TimeSpanSerializesAsMilliseconds]
        public virtual TimeSpan TotalCallTime { get; }

        /// <summary>
        /// Min call time in milliseconds.
        /// ex. 4.835
        /// </summary>
        [JsonArrayIndex(Index = 7)]
        [TimeSpanSerializesAsMilliseconds]
        public virtual TimeSpan MinCallTime { get; }

        /// <summary>
        /// Max call time in milliseconds.
        /// ex. 48.835
        /// </summary>
        [JsonArrayIndex(Index = 8)]
        [TimeSpanSerializesAsMilliseconds]
        public virtual TimeSpan MaxCallTime { get; }

        [JsonArrayIndex(Index = 9)]
        [JsonConverter(typeof(EventAttributesJsonConverter))]
        public virtual IDictionary<string, object> ParameterData { get; }

        public SqlTraceWireModel(string transactionName, string uri, long sqlId, string sql, string datastoreMetricName, uint callCount, TimeSpan totalCallTime, TimeSpan minCallTime, TimeSpan maxCallTime, IDictionary<string, object> parameterData)
        {
            TransactionName = transactionName;
            Uri = uri;
            SqlId = sqlId;
            Sql = sql;
            DatastoreMetricName = datastoreMetricName;
            CallCount = callCount;
            TotalCallTime = totalCallTime;
            MinCallTime = minCallTime;
            MaxCallTime = maxCallTime;
            ParameterData = new ReadOnlyDictionary<string, object>(parameterData);
        }
    }
}
