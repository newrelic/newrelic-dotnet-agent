using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NUnit.Framework;

namespace NewRelic.Agent.Core.WireModels
{
	[TestFixture]
	class SpanEventWireModelTests
	{
		[Test]
		public void SpanEventWireModelTests_Serialization()
		{
			const float priority = 1.975676f;
			const float duration = 4.811791f;
			string ExpectedSerialization =
			$@"[{{""traceId"":""ed5bbf27f28ebef3"",""http.url"":""https://arcane-caverns-89707.herokuapp.com:443"",""http.method"":""GET"",""component"":""Vertx-Client"",""span.kind"":""client"",""category"":""http"",""type"":""Span"",""priority"":{priority:f6},""parentId"":""dead8b84bd93014d"",""duration"":{duration:f6},""transactionId"":""ed5bbf27f28ebef3"",""name"":""External/arcane-caverns-89707.herokuapp.com/Vertx-Client/end"",""guid"":""4aac47f9017ec070"",""sampled"":true,""timestamp"":1523902284677}},{{}},{{}}]";

			var intrinsicAttributes = new Dictionary<string, object>
			{
				{"traceId", "ed5bbf27f28ebef3"},
				{"http.url", "https://arcane-caverns-89707.herokuapp.com:443"},
				{"http.method", "GET"},
				{"component", "Vertx-Client"},
				{"span.kind", "client"},
				{"category", "http"},
				{"type", "Span"},
				{"priority", priority},
				{"parentId", "dead8b84bd93014d"},
				{"duration", duration},
				{"transactionId", "ed5bbf27f28ebef3"},
				{"name", "External/arcane-caverns-89707.herokuapp.com/Vertx-Client/end"},
				{"guid", "4aac47f9017ec070"},
				{"sampled", true},
				{"timestamp", 1523902284677}
			};
			var spanEventWireModel = new SpanEventWireModel(intrinsicAttributes);
			var serialized = JsonConvert.SerializeObject(spanEventWireModel);
			Assert.That(serialized, Is.Not.Null);
			Assert.That(serialized, Is.EqualTo(ExpectedSerialization));
		}
	}
}
