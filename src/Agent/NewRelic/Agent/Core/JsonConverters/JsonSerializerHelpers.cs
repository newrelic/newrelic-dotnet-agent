// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using Newtonsoft.Json;
using NewRelic.Agent.Core.Attributes;
using System.Collections.Generic;
using System.Linq;

namespace NewRelic.Agent.Core.JsonConverters
{
    public static class JsonSerializerHelpers
    {
        public static void WriteCollection(JsonWriter writer, IEnumerable<IAttributeValue> attribValues)
        {
            writer.WriteStartObject();
            if (attribValues != null)
            {
                foreach (var attribVal in attribValues.OrderBy(x => x.AttributeDefinition.Name))
                {
                    //this performs the lazy function (if necessary)
                    //which can result in a null value
                    var outputValue = attribVal.Value;

                    if (outputValue == null)
                    {
                        continue;
                    }

                    writer.WritePropertyName(attribVal.AttributeDefinition.Name);

                    // Causes an exception since this type is unsupported by the JsonConverter
                    if (outputValue.GetType().ToString() == "Microsoft.Extensions.Primitives.StringValues")
                    {
                        writer.WriteValue(outputValue.ToString());
                    }
                    else
                    {
                        writer.WriteValue(outputValue);
                    }
                }
            }

            writer.WriteEndObject();
        }

    }
}
