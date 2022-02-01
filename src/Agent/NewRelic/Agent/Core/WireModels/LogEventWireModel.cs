// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NewRelic.Collections;

namespace NewRelic.Agent.Core.WireModels
{
    public class LogEventWireModel : IHasPriority

    {
        /// <summary>
        /// The UTC timestamp in unix milliseconds. 
        /// </summary>
        public long TimeStamp { get; }

        /// <summary>
        /// The log message.
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// The log level.
        /// </summary>
        public string Level { get; }

        /// <summary>
        /// The span id of the segment.
        /// </summary>
        public string SpanId { get; }

        /// <summary>
        /// The traced if of the transaction.
        /// </summary>
        public string TraceId { get; }

        private float _priority;
        public float Priority
        {
            get { return _priority; }
            set
            {
                const float priorityMin = 0.0f;
                if (value < priorityMin || float.IsNaN(value) || float.IsNegativeInfinity(value) || float.IsPositiveInfinity(value))
                {
                    throw new ArgumentException($"LogEventWireModel requires a valid priority value greater than {priorityMin}, value used: {value}");
                }

                _priority = value;
            }
        }

        public LogEventWireModel(long unixTimestampMS, string message, string level, string spanId, string traceId)
        {
            TimeStamp = unixTimestampMS;
            Message = message;
            Level = level;
            SpanId = spanId;
            TraceId = traceId;
        }
    }
}
