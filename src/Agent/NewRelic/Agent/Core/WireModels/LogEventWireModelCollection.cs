// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using NewRelic.Agent.Core.JsonConverters;
using NewRelic.Agent.Core.Labels;
using Newtonsoft.Json;

namespace NewRelic.Agent.Core.WireModels;

[JsonConverter(typeof(LogEventWireModelCollectionJsonConverter))]
public class LogEventWireModelCollection
{
    public string EntityName { get; }
    public string EntityGuid { get; }
    public string Hostname { get; }
    public IEnumerable<Label> Labels { get; }

    public IList<LogEventWireModel> LoggingEvents { get; }

    public LogEventWireModelCollection(string entityName, string entityGuid, string hostname,
        IEnumerable<Label> labels, IList<LogEventWireModel> loggingEvents)
    {
        EntityName = entityName;
        EntityGuid = entityGuid;
        Hostname = hostname;
        Labels = labels;
        LoggingEvents = loggingEvents;
    }
}
