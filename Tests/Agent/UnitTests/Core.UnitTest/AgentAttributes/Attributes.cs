using System.Linq;
using NewRelic.Agent.Core.Transactions;
using NewRelic.Testing.Assertions;
using NUnit.Framework;

namespace NewRelic.Agent.Core.AgentAttributes
{
	[TestFixture]
	public class Class_Attributes
	{
		private Attributes _attributes;

		private AttributeService _attributeService;

		[SetUp]
		public void SetUp()
		{
			_attributes = new Attributes();
			_attributeService = new AttributeService();
		}

		[Test]
		public void attributes_are_sorted_by_type()
		{
			_attributes.Add(Attribute.BuildOriginalUrlAttribute("banana"));
			_attributes.Add(Attribute.BuildCustomAttribute("pie", "cake"));

			NrAssert.Multiple(
				() => Assert.AreEqual(2, _attributes.Count()),

				() => Assert.AreEqual(1, _attributes.GetAgentAttributes().Count()),
				() => Assert.AreEqual("original_url", _attributes.GetAgentAttributes().First().Key),
				() => Assert.AreEqual("banana", _attributes.GetAgentAttributes().First().Value),
				() => Assert.AreEqual(1, _attributes.GetAgentAttributesDictionary().Count()),
				() => Assert.AreEqual("original_url", _attributes.GetAgentAttributesDictionary().First().Key),
				() => Assert.AreEqual("banana", _attributes.GetAgentAttributesDictionary().First().Value),

				() => Assert.AreEqual(1, _attributes.GetUserAttributes().Count()),
				() => Assert.AreEqual("pie", _attributes.GetUserAttributes().First().Key),
				() => Assert.AreEqual("cake", _attributes.GetUserAttributes().First().Value),
				() => Assert.AreEqual(1, _attributes.GetUserAttributesDictionary().Count()),
				() => Assert.AreEqual("pie", _attributes.GetUserAttributesDictionary().First().Key),
				() => Assert.AreEqual("cake", _attributes.GetUserAttributesDictionary().First().Value)
				);
		}

		[Test]
		public void duplicate_attribute_keys_are_allowed()
		{
			_attributes.Add(Attribute.BuildOriginalUrlAttribute("banana1"));
			_attributes.Add(Attribute.BuildOriginalUrlAttribute("banana2"));
			_attributes.Add(Attribute.BuildCustomAttribute("pie", "cake1"));
			_attributes.Add(Attribute.BuildCustomAttribute("pie", "cake2"));

			NrAssert.Multiple(
				() => Assert.AreEqual(4, _attributes.Count()),

				() => Assert.AreEqual(2, _attributes.GetAgentAttributes().Count()),
				() => Assert.True(_attributes.GetAgentAttributes().Any(attribute => attribute.Value.ToString() == "banana1")),
				() => Assert.True(_attributes.GetAgentAttributes().Any(attribute => attribute.Value.ToString() == "banana2")),

				() => Assert.AreEqual(2, _attributes.GetUserAttributes().Count()),
				() => Assert.True(_attributes.GetUserAttributes().Any(attribute => attribute.Value.ToString() == "cake1")),
				() => Assert.True(_attributes.GetUserAttributes().Any(attribute => attribute.Value.ToString() == "cake2"))
				);
		}

		[Test]
		public void to_dictionary_gives_first_value_when_duplicate_keys_are_present()
		{
			_attributes.Add(Attribute.BuildOriginalUrlAttribute("banana1"));
			_attributes.Add(Attribute.BuildOriginalUrlAttribute("banana2"));
			_attributes.Add(Attribute.BuildCustomAttribute("pie", "cake1"));
			_attributes.Add(Attribute.BuildCustomAttribute("pie", "cake2"));

			NrAssert.Multiple(
				() => Assert.AreEqual(1, _attributes.GetAgentAttributesDictionary().Count()),
				() => Assert.AreEqual("original_url", _attributes.GetAgentAttributesDictionary().First().Key),
				() => Assert.AreEqual("banana1", _attributes.GetAgentAttributesDictionary().First().Value),

				() => Assert.AreEqual(1, _attributes.GetUserAttributesDictionary().Count()),
				() => Assert.AreEqual("pie", _attributes.GetUserAttributesDictionary().First().Key),
				() => Assert.AreEqual("cake1", _attributes.GetUserAttributesDictionary().First().Value)
				);
		}

		[Test]
		public void request_parameter_attributes_are_filtered_out_for_error_events()
		{
			_attributes.Add(Attribute.BuildRequestParameterAttribute("request.parameter.username", "Crash Override"));
			_attributes.Add(Attribute.BuildRequestParameterAttribute("request.parameter.password", "p455w0rd"));

			var filteredAttributes = _attributeService.FilterAttributes(_attributes, AttributeDestinations.ErrorEvent);

			Assert.IsTrue(filteredAttributes.Count() == 0);
		}
	}
}
