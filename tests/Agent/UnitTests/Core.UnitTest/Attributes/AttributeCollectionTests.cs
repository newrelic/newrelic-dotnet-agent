// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
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
            _attribDefSvc = new AttributeDefinitionService((f) => new AttributeDefinitions(f));
        }

        [TearDown]
        public void TearDown()
        {
            _attribDefSvc.Dispose();
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
                () => Assert.That(allAttribs, Has.Count.EqualTo(2)),
                () => Assert.That(agentAttribsDic.Count(), Is.EqualTo(1)),
                () => Assert.That(agentAttribsDic["original_url"], Is.EqualTo("banana")),
                () => Assert.That(userAttribsDic.Count(), Is.EqualTo(1)),
                () => Assert.That(userAttribsDic["pie"], Is.EqualTo("cake"))
            );
        }

        [TestCase(AttributeClassification.Intrinsics)]
        [TestCase(AttributeClassification.AgentAttributes)]
        [TestCase(AttributeClassification.UserAttributes)]
        public void AttributesCanBeUpdatedAfterTypeSpecificLimitIsHit(AttributeClassification attributeType)
        {
            var filter = new AttributeFilter(new AttributeFilter.Settings());

            // Create and set our attribute that will be updated after hitting the type limit
            var attrToUpdate = AttributeDefinitionBuilder
                .CreateLong($"key-to-update-after-limit", attributeType)
                .AppliesTo(AttributeDestinations.All)
                .Build(filter);

            attrToUpdate.TrySetValue(_attribValues, 0);

            // add values until we hit the type specific limit
            for (int i = 1; i < 500; i++)
            {
                var attribDef = AttributeDefinitionBuilder
                    .CreateLong($"testkey-{i}", attributeType)
                    .AppliesTo(AttributeDestinations.All)
                    .Build(filter);

                attribDef.TrySetValue(_attribValues, i);
            }

            // Update our first attribute to over 9000
            attrToUpdate.TrySetValue(_attribValues, 9001);
            var attributeUnderTest = _attribValues.GetAttributeValues(attributeType).FirstOrDefault(x => x.AttributeDefinition.Guid == attrToUpdate.Guid);

            Assert.That(attributeUnderTest, Is.Not.Null);
            Assert.That(attributeUnderTest.Value, Is.EqualTo(9001));
        }

        [TestCase(AttributeClassification.Intrinsics)]
        [TestCase(AttributeClassification.AgentAttributes)]
        [TestCase(AttributeClassification.UserAttributes)]
        public void AttributesCanBeUpdatedAfterGlobalLimitIsHit(AttributeClassification attributeType)
        {
            var filter = new AttributeFilter(new AttributeFilter.Settings());

            // Create and set our attribute that will be updated after hitting the type limit
            var attrToUpdate = AttributeDefinitionBuilder
                .CreateLong($"key-to-update-after-limit", attributeType)
                .AppliesTo(AttributeDestinations.All)
                .Build(filter);

            attrToUpdate.TrySetValue(_attribValues, 0);

            // add values until we hit the type specific limit
            for (int i = 1; i < 500; i++)
            {
                var attribDef = AttributeDefinitionBuilder
                    .CreateLong($"testkey-{i}", attributeType)
                    .AppliesTo(AttributeDestinations.All)
                    .Build(filter);

                attribDef.TrySetValue(_attribValues, i);
            }

            // add intrinsic values until we hit the global limit
            for (int i = 1; i < 500; i++)
            {
                var attribDef = AttributeDefinitionBuilder
                    .CreateLong($"intrinsic-testkey-{i}", AttributeClassification.Intrinsics)
                    .AppliesTo(AttributeDestinations.All)
                    .Build(filter);

                attribDef.TrySetValue(_attribValues, i);
            }

            // Update our first attribute to over 9000
            attrToUpdate.TrySetValue(_attribValues, 9001);
            var attributeUnderTest = _attribValues.GetAttributeValues(attributeType).FirstOrDefault(x => x.AttributeDefinition.Guid == attrToUpdate.Guid);

            Assert.That(attributeUnderTest, Is.Not.Null);
            Assert.That(attributeUnderTest.Value, Is.EqualTo(9001));
        }
        
        [TestCase(AttributeClassification.Intrinsics, 255)]
        [TestCase(AttributeClassification.AgentAttributes, 255)]
        [TestCase(AttributeClassification.UserAttributes, 64)]
        public void AttributeLimitsAreEnforced(AttributeClassification attributeType, int expectedLimit)
        {
            // Commentary: I'm not sure parameterizing this test is very supportable. This unit test needs a unit test.
            var filter = new AttributeFilter(new AttributeFilter.Settings());

            for (int i = 1; i < expectedLimit * 2; i++)
            {
                var attribDef = AttributeDefinitionBuilder
                    .CreateLong($"testkey-{i}", attributeType)
                    .AppliesTo(AttributeDestinations.All)
                    .Build(filter);

                attribDef.TrySetValue(_attribValues, i);
            }

            // Just select all the values which will make validation easier
            var attributeValues = _attribValues.GetAttributeValues(attributeType).Select(x => x.Value).ToArray();

            NrAssert.Multiple
            (
                () => Assert.That(attributeValues, Has.Length.EqualTo(expectedLimit)),
                () => Assert.That(attributeValues, Does.Contain(1)),
                () => Assert.That(attributeValues, Does.Contain(expectedLimit)),
                () => Assert.That(attributeValues, Does.Not.Contain(expectedLimit + 1), "A wrong attribute was captured :(")
            );

            // verify that trying to add more of each attribute type has no effect
            _attribDefs.GetCustomAttributeForTransaction("ShouldNotDoAnything").TrySetValue(_attribValues, "nothx");
            var userAttributeKeys = _attribValues.GetAttributeValues(AttributeClassification.UserAttributes).Select(x => x.AttributeDefinition.Name).ToArray();
            Assert.That(userAttributeKeys, Does.Not.Contain("ShouldNotDoAnything"));

            // This is an Agent attribute
            _attribDefs.OriginalUrl.TrySetValue(_attribValues, "nothx");
            var agentAttributeKeys = _attribValues.GetAttributeValues(AttributeClassification.AgentAttributes).Select(x => x.AttributeDefinition.Name).ToArray();
            if (attributeType == AttributeClassification.UserAttributes)
            {
                // The user attribute limit is lower than the agent attribute limit, so we expect to see the new attribute in this case
                Assert.That(agentAttributeKeys, Does.Contain(_attribDefs.OriginalUrl.Name));
            }
            else
            {
                Assert.That(agentAttributeKeys, Does.Not.Contain(_attribDefs.OriginalUrl.Name));
            }

            // This is an Intrinsic attribute
            _attribDefs.TimestampForError.TrySetValue(_attribValues, DateTime.Now);
            var intrinsicAttributeKeys = _attribValues.GetAttributeValues(AttributeClassification.Intrinsics).Select(x => x.AttributeDefinition.Name).ToArray();
            if (attributeType == AttributeClassification.UserAttributes)
            {
                // The user attribute limit is lower than the intrinsic attribute limit, so we expect to see the new attribute in this case
                Assert.That(intrinsicAttributeKeys, Does.Contain(_attribDefs.TimestampForError.Name));
            }
            else
            {
                Assert.That(intrinsicAttributeKeys, Does.Not.Contain(_attribDefs.TimestampForError.Name));
            }
        }

        [Test]
        public void DuplicateAttributeKeepsLastValue()
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
                () => Assert.That(allAttribs, Has.Count.EqualTo(2)),
                () => Assert.That(agentAttribsDic.Count(), Is.EqualTo(1)),
                () => Assert.That(agentAttribsDic[_attribDefs.OriginalUrl.Name], Is.EqualTo("banana2")),
                () => Assert.That(userAttribsDic.Count(), Is.EqualTo(1)),
                () => Assert.That(userAttribsDic[_attribDefs.GetCustomAttributeForTransaction("pie").Name], Is.EqualTo("cake2"))
            );
        }

        /*
         *  Size of AttributeValue
         *  Null Custom Attrib not supported
         *  null is filtered
         *  Filtering works(attrib1 attrib2 diff destinations)
         */

        [Test]
        public void request_parameter_attributes_are_filtered_out_for_error_events()
        {
            // _attribValues is an error event collection, which should not receive request parameters by default (must be enabled in config)
            _attribDefs.GetRequestParameterAttribute("CheckOne").TrySetValue(_attribValues, "One");
            _attribDefs.GetRequestParameterAttribute("CheckTwo").TrySetValue(_attribValues, "Two");

            var allAttribs = _attribValues.GetAttributeValues(AttributeClassification.Intrinsics)
                .Union(_attribValues.GetAttributeValues(AttributeClassification.AgentAttributes))
                .Union(_attribValues.GetAttributeValues(AttributeClassification.UserAttributes));

            Assert.That(allAttribs, Is.Empty);
        }


        private const string UserValue = "uservalue";
        private const string Intrinsicvalue = "intrinsicvalue";
        private const string Agentvalue = "agentvalue";
        [TestCase("userOnly", UserValue)]
        [TestCase("transactionName", Intrinsicvalue)]
        [TestCase("original_url", Agentvalue)]
        public void GetAllAttributeValuesDic_AgentOverIntrinsicOverUser(string userKey, string expectedResult)
        {
            // user
            _attribDefs.GetCustomAttributeForTransaction(userKey).TrySetValue(_attribValues, UserValue);

            // intrinsic key=transactionName
            _attribDefs.TransactionNameForError.TrySetValue(_attribValues, Intrinsicvalue);

            // agent key=original_url
            _attribDefs.OriginalUrl.TrySetValue(_attribValues, Agentvalue);

            var allValues = _attribValues.GetAllAttributeValuesDic();

            Assert.That(allValues[userKey].ToString(), Is.EqualTo(expectedResult));
        }
    }
}
