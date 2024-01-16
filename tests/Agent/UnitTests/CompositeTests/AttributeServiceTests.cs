// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Core.Attributes;
using NewRelic.Agent.Core.Transactions;
using NewRelic.Agent.TestUtilities;
using NewRelic.Testing.Assertions;

namespace CompositeTests
{
    public class AttributeServiceTests
    {
        private CompositeTestAgent _compositeTestAgent;
        private IAttributeValueCollection _attribValues;
        private IAttributeDefinitions _attribDefs => _compositeTestAgent?.AttributeDefinitions;
        

        [SetUp]
        public void SetUp()
        {
            _compositeTestAgent = new CompositeTestAgent();
           
            _attribValues = new AttributeValueCollection(AttributeDestinations.TransactionEvent);
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

            _attribDefs.TransactionName.TrySetValue(_attribValues,"name");
            _attribDefs.HostDisplayName.TrySetValue(_attribValues, "host");
            _attribDefs.GetCustomAttributeForTransaction("custom1Name").TrySetValue(_attribValues, "custom1Value");

            var expectedIntrisicDictionary = new Dictionary<string, object> { { "name", "name" } };
            NrAssert.Multiple
            (
                () => CollectionAssert.AreEquivalent(expectedIntrisicDictionary, _attribValues.ToDictionary(AttributeClassification.Intrinsics)),
                () => CollectionAssert.IsEmpty(_attribValues.ToDictionary(AttributeClassification.AgentAttributes), "Agent attributes were not empty."),
                () => CollectionAssert.IsEmpty(_attribValues.ToDictionary(AttributeClassification.UserAttributes), "Custom attributes were not empty.")
            );
        }

        [Test]
        public void only_agent_and_custom_attributes_should_be_filtered_when_all_attributes_excluded()
        {
            _compositeTestAgent.LocalConfiguration.attributes.exclude = new List<string> { "*" };
            _compositeTestAgent.PushConfiguration();

            _attribDefs.TransactionName.TrySetValue(_attribValues, "name");
            _attribDefs.HostDisplayName.TrySetValue(_attribValues, "host");
            _attribDefs.GetCustomAttributeForTransaction("custom1Name").TrySetValue(_attribValues, "custom1Value");


            var expectedIntrisicDictionary = new Dictionary<string, object> { { "name", "name" } };

            NrAssert.Multiple
            (
                () => CollectionAssert.AreEquivalent(expectedIntrisicDictionary, _attribValues.ToDictionary(AttributeClassification.Intrinsics)),
                () => CollectionAssert.IsEmpty(_attribValues.ToDictionary(AttributeClassification.AgentAttributes), "Agent attributes were not empty."),
                () => CollectionAssert.IsEmpty(_attribValues.ToDictionary(AttributeClassification.UserAttributes), "Custom attributes were not empty.")
            );
        }

        [Test]
        public void all_attributes_included()
        {
            _attribDefs.TransactionName.TrySetValue(_attribValues, "name");
            _attribDefs.HostDisplayName.TrySetValue(_attribValues, "host");
            _attribDefs.GetCustomAttributeForTransaction("custom1Name").TrySetValue(_attribValues, "custom1Value");

            var expectedIntrisicDictionary = new Dictionary<string, object> { { "name", "name" } };
            var expectedAgentDictionary = new Dictionary<string, object> { { "host.displayName", "host" } };
            var expectedUserDictionary = new Dictionary<string, object> { { "custom1Name", "custom1Value" } };
            NrAssert.Multiple
            (
                () => CollectionAssert.AreEquivalent(expectedIntrisicDictionary, _attribValues.ToDictionary(AttributeClassification.Intrinsics)),
                () => CollectionAssert.AreEquivalent(expectedAgentDictionary, _attribValues.ToDictionary(AttributeClassification.AgentAttributes)),
                () => CollectionAssert.AreEquivalent(expectedUserDictionary, _attribValues.ToDictionary(AttributeClassification.UserAttributes))
            );
        }

        [Test]
        public void null_values_should_be_excluded()
        {
            _attribDefs.TransactionName.TrySetValue(_attribValues, null as string);
            _attribDefs.HostDisplayName.TrySetValue(_attribValues, null as string);
            _attribDefs.GetCustomAttributeForTransaction("custom1Name").TrySetValue(_attribValues, null);

            ClassicAssert.AreEqual(0, _attribValues.ToDictionary().Count);
        }
    }
}
