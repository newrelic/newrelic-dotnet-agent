using Newtonsoft.Json;
using NewRelic.Agent.Core.Attributes;
using System;
using System.Collections.Generic;
using NewRelic.Agent.Core.Segments;

namespace NewRelic.Agent.Core.JsonConverters
{

	public class SpanEventWireModelSerializer : JsonConverter<ISpanEventWireModel>
	{
		public override ISpanEventWireModel ReadJson(JsonReader reader, Type objectType, ISpanEventWireModel existingValue, bool hasExistingValue, JsonSerializer serializer)
		{
			throw new NotImplementedException();
		}

		public override void WriteJson(JsonWriter writer, ISpanEventWireModel value, JsonSerializer serializer)
		{
			writer.WriteStartArray();
			WriteCollection(writer, value.GetAttributeValues(AttributeClassification.Intrinsics));
			WriteCollection(writer, value.GetAttributeValues(AttributeClassification.UserAttributes));
			WriteCollection(writer, value.GetAttributeValues(AttributeClassification.AgentAttributes));
			writer.WriteEndArray();
		}

		private void WriteCollection(JsonWriter writer, IEnumerable< IAttributeValue> attribValues)
		{
			writer.WriteStartObject();
			if (attribValues != null)
			{
				foreach (var attribVal in attribValues)
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