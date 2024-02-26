// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Core.Attributes;
using NewRelic.Agent.Core.JsonConverters;
using Newtonsoft.Json;

namespace NewRelic.Agent.Core.WireModels
{
    [JsonConverter(typeof(EventWireModelSerializer))]
    public interface IEventWireModel : IWireModel
    {
        IAttributeValueCollection AttributeValues { get; }
    }
}
