// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Core.JsonConverters;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace NewRelic.Agent.Core.WireModels
{
    [JsonConverter(typeof(LogEventWireModelCollectionJsonConverter))]
    public class LogEventWireModelCollection
    {
        public string EntityGuid { get; }

        public string Hostname { get; }

        public IList<LogEventWireModel> LoggingEvents { get; }

        public LogEventWireModelCollection(string entityGuid, string hostname, IList<LogEventWireModel> loggingEvents)
        {
            EntityGuid = entityGuid;
            Hostname = hostname;
            LoggingEvents = loggingEvents;
        }
    }
}
