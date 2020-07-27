using System;
using System.Collections;
using System.Collections.Generic;
using JetBrains.Annotations;
using NewRelic.Agent.Core.DataTransport;
using NewRelic.Agent.Core.JsonConverters;
using NewRelic.SystemExtensions.Collections.Generic;
using Newtonsoft.Json;

namespace NewRelic.Agent.Core.WireModels
{
    /// <summary>
    /// Jsonable object containing all of the things necessary to serialize a SQL Trace Data for the sql_trace_data collector command.
    /// </summary>
    /// <remarks>https://pdx-hudson.datanerd.us/job/collector-master/javadoc/com/nr/entities/SqlTrace.html</remarks>
    [JsonConverter(typeof(JsonArrayConverter))]
    public class SqlTraceWireModel
    {
        /// <summary>
        /// ex. WebTransaction/ASP/post.aspx
        /// </summary>
        [JsonArrayIndex(Index = 0)]
        [NotNull] public virtual String TransactionName { get; }

        /// <summary>
        /// ex. http://localhost:8080/post.aspx
        /// </summary>
        [JsonArrayIndex(Index = 1)]
        [NotNull] public virtual String Uri { get; }

        /// <summary>
        /// The hash code of the obfuscated sql string.
        /// ex. 1530282818
        /// </summary>
        [JsonArrayIndex(Index = 2)]
        public virtual Int64 SqlId { get; }

        /// <summary>
        /// The sql string for the slowest statement.
        /// ex. DELETE FROM be_DataStoreSettings WHERE ExtensionType = @type AND ExtensionId = @id; 
        /// </summary>        
        [JsonArrayIndex(Index = 3)]
        [NotNull] public virtual String Sql { get; }

        /// <summary>
        /// The name of the database metric that this sql statement is associated with.
        /// ex. Datastore/statement/MySQL/be_DataStoreSettings/DELETE
        /// </summary>
        [JsonArrayIndex(Index = 4)]
        [NotNull] public virtual String DatastoreMetricName { get; }

        /// <summary>
        /// Total call count ex. 1
        /// </summary>
        [JsonArrayIndex(Index = 5)]
        public virtual UInt32 CallCount { get; }

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
        [NotNull]
        public virtual IDictionary<String, Object> ParameterData { get; }

        public SqlTraceWireModel([NotNull] String transactionName, [NotNull] String uri, Int64 sqlId, [NotNull] String sql, [NotNull] String datastoreMetricName, UInt32 callCount, TimeSpan totalCallTime, TimeSpan minCallTime, TimeSpan maxCallTime, [NotNull] IEnumerable<KeyValuePair<String, Object>> parameterData)
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
            ParameterData = new ReadOnlyDictionary<String, Object>(parameterData.ToDictionary());
        }
    }
}
