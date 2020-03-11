using NewRelic.Agent.Core.Attributes;
using NewRelic.Agent.TestUtilities;
using Newtonsoft.Json;
using NUnit.Framework;
using System.Collections.Generic;
using Attribute = NewRelic.Agent.Core.Attributes.Attribute;

namespace NewRelic.Agent.Core.Spans.Tests
{
	[TestFixture]
	class SpanEventWireModelTests
	{
		[Test]
		public void SpanEventWireModelTests_Serialization()
		{
			const float priority = 1.975676f;
			//var ExpectedSerialization =
			//	$@"[{{""type"":""Span"",""priority"":{priority:f6},""traceId"":""ed5bbf27f28ebef3""}},{{}},{{""http.method"":""GET""}}]";

			var expectedSerializationDic = new Dictionary<string, object>[3]
			{
				new Dictionary<string, object>()
				{
					{"type", "Span" },
					{"priority", priority },
					{"traceId", "ed5bbf27f28ebef3" },
				},
				new Dictionary<string, object>()
				{

				},
				new Dictionary<string, object>()
				{
					{"http.method", "GET" }
				}
			};


			var attributes = new AttributeCollection();
			attributes.Add(Attribute.BuildTypeAttribute(TypeAttributeValue.Span));
			attributes.Add(Attribute.BuildPriorityAttribute(priority));
			attributes.Add(Attribute.BuildDistributedTraceIdAttributes("ed5bbf27f28ebef3"));
			attributes.Add(Attribute.BuildHttpMethodAttribute("GET"));

			var spanEventWireModel = new SpanEventWireModel(priority, attributes.GetIntrinsicsDictionary(), attributes.GetUserAttributesDictionary(), attributes.GetAgentAttributesDictionary());
			var serialized = JsonConvert.SerializeObject(spanEventWireModel);
			Assert.That(serialized, Is.Not.Null);

			var deserialized = JsonConvert.DeserializeObject<Dictionary<string, object>[]>(serialized);
			Assert.That(deserialized, Is.Not.Null);

			DictionaryComparer.CompareDictionaries(expectedSerializationDic, deserialized);
		}
	}
}
