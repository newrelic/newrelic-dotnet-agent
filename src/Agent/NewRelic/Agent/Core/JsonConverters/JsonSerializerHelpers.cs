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
                    writer.WriteValue(outputValue);
                }
            }
            writer.WriteEndObject();
        }

    }
}
