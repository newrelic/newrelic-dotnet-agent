// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Core.Attributes;
using NewRelic.Agent.Core.Segments;
using System.Collections.ObjectModel;

namespace NewRelic.Agent.TestUtilities
{
    public static class SpanEventWireModelExtensionMethods
    {
        public static ReadOnlyDictionary<string, object> IntrinsicAttributes(this ISpanEventWireModel spanEvent) => ConvertAttribValuesToDictionary(spanEvent, AttributeClassification.Intrinsics);

        public static ReadOnlyDictionary<string, object> UserAttributes(this ISpanEventWireModel spanEvent) => ConvertAttribValuesToDictionary(spanEvent, AttributeClassification.UserAttributes);

        public static ReadOnlyDictionary<string, object> AgentAttributes(this ISpanEventWireModel spanEvent) => ConvertAttribValuesToDictionary(spanEvent, AttributeClassification.AgentAttributes);

        private static ReadOnlyDictionary<string, object> ConvertAttribValuesToDictionary(ISpanEventWireModel spanEvent, AttributeClassification classification)
        {
            return new ReadOnlyDictionary<string, object>(spanEvent
                .GetAttributeValues(classification)
                .ToDictionary(x => x.AttributeDefinition.Name, x => x.Value));
        }
    }
}
