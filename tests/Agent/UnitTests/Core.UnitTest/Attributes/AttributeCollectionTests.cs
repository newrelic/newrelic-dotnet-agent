// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Linq;
using NewRelic.Testing.Assertions;
using NUnit.Framework;

namespace NewRelic.Agent.Core.Attributes.Tests
{
    [TestFixture]
    public class AttributeCollectionTests
    {
        private IAttributeValueCollection _attribValues;
        private IAttributeDefinitionService _attribDefSvc;
        private IAttributeDefinitions _attribDefs => _attribDefSvc?.AttributeDefs;

        [SetUp]
        public void SetUp()
        {
            _attribValues = new AttributeValueCollection(AttributeDestinations.ErrorEvent);
            _attribDefSvc = new AttributeDefinitionService((f)=>new AttributeDefinitions(f));
        }

        [Test]
        public void AttributeClassificationIsCorrect()
        {
            _attribDefs.OriginalUrl.TrySetValue(_attribValues, "banana");
            _attribDefs.GetCustomAttributeForTransaction("pie").TrySetValue(_attribValues, "cake");

            var agentAttribsDic = _attribValues.GetAttributeValuesDic(AttributeClassification.AgentAttributes);
            var userAttribsDic = _attribValues.GetAttributeValuesDic(AttributeClassification.UserAttributes);
            var allAttribs = _attribValues.GetAttributeValues(AttributeClassification.Intrinsics)
                .Union(_attribValues.GetAttributeValues(AttributeClassification.AgentAttributes))
                .Union(_attribValues.GetAttributeValues(AttributeClassification.UserAttributes))
                .ToList();

            NrAssert.Multiple
            (
                () => Assert.AreEqual(2, allAttribs.Count),
                () => Assert.AreEqual(1, agentAttribsDic.Count()),
                () => Assert.AreEqual("banana", agentAttribsDic["original_url"]),
                () => Assert.AreEqual(1, userAttribsDic.Count()),
                () => Assert.AreEqual("cake", userAttribsDic["pie"])
            );
        }

        [Test]
        public void DuplicateAttibuteKeepsLastValue()
        {
            _attribDefs.OriginalUrl.TrySetValue(_attribValues, "banana1");
            _attribDefs.OriginalUrl.TrySetValue(_attribValues, "banana2");
            _attribDefs.GetCustomAttributeForTransaction("pie").TrySetValue(_attribValues, "cake1");
            _attribDefs.GetCustomAttributeForTransaction("pie").TrySetValue(_attribValues, "cake2");

            var agentAttribsDic = _attribValues.GetAttributeValuesDic(AttributeClassification.AgentAttributes);
            var userAttribsDic = _attribValues.GetAttributeValuesDic(AttributeClassification.UserAttributes);
            var allAttribs = _attribValues.GetAttributeValues(AttributeClassification.Intrinsics)
                .Union(_attribValues.GetAttributeValues(AttributeClassification.AgentAttributes))
                .Union(_attribValues.GetAttributeValues(AttributeClassification.UserAttributes))
                .ToList();

            NrAssert.Multiple
            (
                () => Assert.AreEqual(4, allAttribs.Count),
                () => Assert.AreEqual(1, agentAttribsDic.Count()),
                () => Assert.AreEqual("banana2", agentAttribsDic["original_url"]),
                () => Assert.AreEqual(1, userAttribsDic.Count()),
                () => Assert.AreEqual("cake2", userAttribsDic["pie"])
            );
        }

        /*
         *  Count of Attributes
         *  Size of AttributeValue
         *  Null Custom Attrib not supported
         *  null is filtered
         *  Filtering works(attrib1 attrib2 diff destinatinos)
         */

        [Test]
        public void request_parameter_attributes_are_filtered_out_for_error_events()
        {
            var attribValues = new AttributeValueCollection(AttributeDestinations.ErrorEvent, AttributeDestinations.TransactionEvent);

            _attribDefs.GetRequestParameterAttribute("username").TrySetValue(attribValues, "Crash Override");
            _attribDefs.GetRequestParameterAttribute("password").TrySetValue(attribValues, "p455w0rd");

            var filteredAttribValues = new AttributeValueCollection(AttributeDestinations.ErrorEvent);

            var allAttribs = filteredAttribValues.GetAttributeValues(AttributeClassification.Intrinsics)
                .Union(filteredAttribValues.GetAttributeValues(AttributeClassification.AgentAttributes))
                .Union(filteredAttribValues.GetAttributeValues(AttributeClassification.UserAttributes))
                .ToList();

            CollectionAssert.IsEmpty(allAttribs);
        }
    }
}
