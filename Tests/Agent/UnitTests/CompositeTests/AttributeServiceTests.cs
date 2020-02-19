using System.Collections.Generic;
using NewRelic.Agent.Core.Attributes;
using NewRelic.Agent.Core.Transactions;
using NewRelic.Testing.Assertions;
using NUnit.Framework;

namespace CompositeTests
{
	public class AttributeServiceTests
	{
		private CompositeTestAgent _compositeTestAgent;
		private AttributeCollection _attributes;
		private AttributeService _attributeService;

		[SetUp]
		public void SetUp()
		{
			_compositeTestAgent = new CompositeTestAgent();
			_attributes = new AttributeCollection();
			_attributeService = new AttributeService();
		}

		[TearDown]
		public void TearDown()
		{
			_compositeTestAgent.Dispose();
		}

		[Test]
		public void only_agent_and_custom_attributes_should_be_filtered_when_attributes_disabled()
		{
			_compositeTestAgent.LocalConfiguration.attributes.enabled = false;
			_compositeTestAgent.PushConfiguration();
			_attributes.Add(Attribute.BuildTransactionNameAttribute("name"));
			_attributes.Add(Attribute.BuildHostDisplayNameAttribute("host"));
			_attributes.Add(Attribute.BuildCustomAttribute("custom1Name", "custom1Value"));

			var filteredAttributes = _attributeService.FilterAttributes(_attributes, AttributeDestinations.TransactionEvent);

			var expectedIntrisicDictionary = new Dictionary<string, object> { { "name", "name" } };
			NrAssert.Multiple(
				() => CollectionAssert.AreEquivalent(expectedIntrisicDictionary, filteredAttributes.GetIntrinsicsDictionary()),
				() => CollectionAssert.IsEmpty(filteredAttributes.GetAgentAttributesDictionary(), "Agent attributes were not empty."),
				() => CollectionAssert.IsEmpty(filteredAttributes.GetUserAttributesDictionary(), "Custom attributes were not empty.")
			);
		}

		[Test]
		public void only_agent_and_custom_attributes_should_be_filtered_when_all_attributes_excluded()
		{
			_compositeTestAgent.LocalConfiguration.attributes.exclude = new List<string> { "*" };
			_compositeTestAgent.PushConfiguration();
			_attributes.Add(Attribute.BuildTransactionNameAttribute("name"));
			_attributes.Add(Attribute.BuildHostDisplayNameAttribute("host"));
			_attributes.Add(Attribute.BuildCustomAttribute("custom1Name", "custom1Value"));

			var filteredAttributes = _attributeService.FilterAttributes(_attributes, AttributeDestinations.TransactionEvent);

			var expectedIntrisicDictionary = new Dictionary<string, object> { { "name", "name" } };
			NrAssert.Multiple(
				() => CollectionAssert.AreEquivalent(expectedIntrisicDictionary, filteredAttributes.GetIntrinsicsDictionary()),
				() => CollectionAssert.IsEmpty(filteredAttributes.GetAgentAttributesDictionary(), "Agent attributes were not empty"),
				() => CollectionAssert.IsEmpty(filteredAttributes.GetUserAttributesDictionary(), "Custom attributes were not empty.")
			);
		}

		[Test]
		public void all_attributes_included()
		{
			_attributes.Add(Attribute.BuildTransactionNameAttribute("name"));
			_attributes.Add(Attribute.BuildHostDisplayNameAttribute("host"));
			_attributes.Add(Attribute.BuildCustomAttribute("custom1Name", "custom1Value"));

			var filteredAttributes = _attributeService.FilterAttributes(_attributes, AttributeDestinations.TransactionEvent);

			var expectedIntrisicDictionary = new Dictionary<string, object> { { "name", "name" } };
			var expectedAgentDictionary = new Dictionary<string, object> { { "host.displayName", "host" } };
			var expectedUserDictionary = new Dictionary<string, object> { { "custom1Name", "custom1Value" } };
			NrAssert.Multiple(
				() => CollectionAssert.AreEquivalent(expectedIntrisicDictionary, filteredAttributes.GetIntrinsicsDictionary()),
				() => CollectionAssert.AreEquivalent(expectedAgentDictionary, filteredAttributes.GetAgentAttributesDictionary()),
				() => CollectionAssert.AreEquivalent(expectedUserDictionary, filteredAttributes.GetUserAttributesDictionary())
			);
		}

		[Test]
		public void null_values_should_be_excluded()
		{
			_attributes.Add(Attribute.BuildTransactionNameAttribute(null));
			_attributes.Add(Attribute.BuildHostDisplayNameAttribute(null));
			_attributes.Add(Attribute.BuildCustomAttribute("custom1Name", null));

			var filteredAttributes = _attributeService.FilterAttributes(_attributes, AttributeDestinations.TransactionEvent);

			Assert.AreEqual(0, filteredAttributes.Count());
		}
	}
}
