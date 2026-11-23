// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.Core.Segments;
using NewRelic.Testing.Assertions;
using NUnit.Framework;
using Serilog.Events;

namespace NewRelic.Agent.Core.Attributes.Tests;

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
        _attribDefs.TransactionCustomAttributes.TrySetValue(_attribValues, "pie", "cake");

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
        var attributeUnderTest = _attribValues.GetAttributeValues(attributeType).FirstOrDefault(x => x.Key.Equals(new AttributeKey(attrToUpdate.Guid, null)));

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
        var attributeUnderTest = _attribValues.GetAttributeValues(attributeType).FirstOrDefault(x => x.Key.Equals(new AttributeKey(attrToUpdate.Guid, null)));

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
        _attribDefs.TransactionCustomAttributes.TrySetValue(_attribValues, "ShouldNotDoAnything", "nothx");
        var userAttributeKeys = _attribValues.GetAttributeValues(AttributeClassification.UserAttributes).Select(x => x.Name).ToArray();
        Assert.That(userAttributeKeys, Does.Not.Contain("ShouldNotDoAnything"));

        // This is an Agent attribute
        _attribDefs.OriginalUrl.TrySetValue(_attribValues, "nothx");
        var agentAttributeKeys = _attribValues.GetAttributeValues(AttributeClassification.AgentAttributes).Select(x => x.Name).ToArray();
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
        var intrinsicAttributeKeys = _attribValues.GetAttributeValues(AttributeClassification.Intrinsics).Select(x => x.Name).ToArray();
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
        _attribDefs.TransactionCustomAttributes.TrySetValue(_attribValues, "pie", "cake1");
        _attribDefs.TransactionCustomAttributes.TrySetValue(_attribValues, "pie", "cake2");

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
            () => Assert.That(userAttribsDic[_attribDefs.TransactionCustomAttributes.Resolve("pie").Name], Is.EqualTo("cake2"))
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
        _attribDefs.RequestParameterAttributes.TrySetValue(_attribValues, "CheckOne", "One");
        _attribDefs.RequestParameterAttributes.TrySetValue(_attribValues, "CheckTwo", "Two");

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
        _attribDefs.TransactionCustomAttributes.TrySetValue(_attribValues, userKey, UserValue);

        // intrinsic key=transactionName
        _attribDefs.TransactionNameForError.TrySetValue(_attribValues, Intrinsicvalue);

        // agent key=original_url
        _attribDefs.OriginalUrl.TrySetValue(_attribValues, Agentvalue);

        var allValues = _attribValues.GetAllAttributeValuesDic();

        Assert.That(allValues[userKey].ToString(), Is.EqualTo(expectedResult));
    }

    #region Family values

    private static AttributeDefinition CreateFamilyDefinition(AttributeClassification classification = AttributeClassification.UserAttributes)
    {
        return new AttributeDefinition("test.family.*", classification, new Dictionary<AttributeDestinations, bool>());
    }

    private static ResolvedAttributeName Resolve(string name, AttributeClassification classification = AttributeClassification.UserAttributes, AttributeDestinations destinations = AttributeDestinations.All)
    {
        return new ResolvedAttributeName(name, classification, destinations);
    }

    private static AttributeDefinition<object, object> CreateStaticUserDefinition(string name)
    {
        return AttributeDefinitionBuilder
            .Create<object>(name, AttributeClassification.UserAttributes)
            .AppliesTo(AttributeDestinations.All)
            .Build(new AttributeFilter(new AttributeFilter.Settings()));
    }

    [Test]
    public void FamilySet_TwoNames_TwoValues()
    {
        var family = CreateFamilyDefinition();

        var setFoo = _attribValues.TrySetValue(family, Resolve("foo"), "one");
        var setBar = _attribValues.TrySetValue(family, Resolve("bar"), "two");

        var values = _attribValues.GetAttributeValuesDic(AttributeClassification.UserAttributes);
        Assert.Multiple(() =>
        {
            Assert.That(setFoo, Is.True);
            Assert.That(setBar, Is.True);
            Assert.That(_attribValues.Count, Is.EqualTo(2));
            Assert.That(values, Has.Count.EqualTo(2));
            Assert.That(values["foo"], Is.EqualTo("one"));
            Assert.That(values["bar"], Is.EqualTo("two"));
        });
    }

    [Test]
    public void FamilySet_StoresResolvedDestinationsOnValue()
    {
        var family = CreateFamilyDefinition();

        _attribValues.TrySetValue(family, Resolve("foo", destinations: AttributeDestinations.SpanEvent), "one");

        var value = _attribValues.GetAttributeValues(AttributeClassification.UserAttributes).Single();
        Assert.Multiple(() =>
        {
            Assert.That(value.Name, Is.EqualTo("foo"));
            Assert.That(value.AttributeDestinations, Is.EqualTo(AttributeDestinations.SpanEvent));
        });
    }

    [Test]
    public void FamilySet_SameName_Overwrites_CountUnchanged()
    {
        var family = CreateFamilyDefinition();

        _attribValues.TrySetValue(family, Resolve("foo"), "one");
        var overwritten = _attribValues.TrySetValue(family, Resolve("foo"), "two");

        var values = _attribValues.GetAttributeValuesDic(AttributeClassification.UserAttributes);
        Assert.Multiple(() =>
        {
            Assert.That(overwritten, Is.False);
            Assert.That(_attribValues.Count, Is.EqualTo(1));
            Assert.That(values["foo"], Is.EqualTo("two"));
        });
    }

    [Test]
    public void FamilySet_UserLimit_UsesKey()
    {
        var family = CreateFamilyDefinition();
        for (var i = 1; i <= AttributeValueCollectionBase<AttributeValue>.MaxCountUserAttrib; i++)
        {
            Assert.That(_attribValues.TrySetValue(family, Resolve($"name{i}"), i), Is.True);
        }

        var rejected = _attribValues.TrySetValue(family, Resolve("name65"), 65);
        _attribValues.TrySetValue(family, Resolve("name1"), 1000);

        var values = _attribValues.GetAttributeValuesDic(AttributeClassification.UserAttributes);
        Assert.Multiple(() =>
        {
            Assert.That(rejected, Is.False);
            Assert.That(values, Has.Count.EqualTo(64));
            Assert.That(values.ContainsKey("name65"), Is.False);
            Assert.That(values["name1"], Is.EqualTo(1000));
            Assert.That(_attribValues.Count, Is.EqualTo(64));
        });
    }

    [Test]
    public void FamilySet_TwoDefinitions_SameName_TwoValues()
    {
        var transactionFamily = CreateFamilyDefinition();
        var errorFamily = CreateFamilyDefinition();

        _attribValues.TrySetValue(transactionFamily, Resolve("foo"), "tx");
        _attribValues.TrySetValue(errorFamily, Resolve("foo"), "err");

        var values = _attribValues.GetAttributeValues(AttributeClassification.UserAttributes).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(values, Has.Count.EqualTo(2));
            Assert.That(values.Select(x => x.Value), Is.EquivalentTo(new object[] { "tx", "err" }));
            Assert.That(_attribValues.Count, Is.EqualTo(2));
        });
    }

    [Test]
    public void FamilyAndStatic_SameName_TwoValues()
    {
        var family = CreateFamilyDefinition();
        var staticDefinition = CreateStaticUserDefinition("foo");

        staticDefinition.TrySetValue(_attribValues, "static");
        _attribValues.TrySetValue(family, Resolve("foo"), "family");

        var values = _attribValues.GetAttributeValues(AttributeClassification.UserAttributes).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(values, Has.Count.EqualTo(2));
            Assert.That(values.Select(x => x.Value), Is.EquivalentTo(new object[] { "static", "family" }));
            Assert.That(values.Select(x => x.Key.Name), Is.EquivalentTo(new string[] { null, "foo" }));
        });
    }

    [Test]
    public void GetAllAttributeValuesDic_FamilyValues_UseValueNames()
    {
        var family = CreateFamilyDefinition();
        _attribValues.TrySetValue(family, Resolve("foo"), "one");

        var all = _attribValues.GetAllAttributeValuesDic();

        Assert.That(all["foo"], Is.EqualTo("one"));
    }

    [Test]
    public void AddRange_FiltersByValueDestinations()
    {
        var family = CreateFamilyDefinition();
        var values = new List<IAttributeValue>
        {
            new AttributeValue(family, "span", AttributeDestinations.SpanEvent) { Value = 1 },
            new AttributeValue(family, "transaction", AttributeDestinations.TransactionEvent) { Value = 2 },
        };
        var destination = new AttributeValueCollection(AttributeDestinations.TransactionEvent);

        destination.AddRange(values);

        var copied = destination.GetAttributeValuesDic(AttributeClassification.UserAttributes);
        Assert.Multiple(() =>
        {
            Assert.That(copied, Has.Count.EqualTo(1));
            Assert.That(copied.ContainsKey("transaction"), Is.True);
        });
    }

    [Test]
    public void RemoveItems_RemovesByKey()
    {
        var family = CreateFamilyDefinition();
        _attribValues.TrySetValue(family, Resolve("foo"), null);
        _attribValues.TrySetValue(family, Resolve("bar"), "kept");

        _attribValues.MakeImmutable();

        var remaining = _attribValues.GetAttributeValuesDic(AttributeClassification.UserAttributes);
        Assert.Multiple(() =>
        {
            Assert.That(remaining, Has.Count.EqualTo(1));
            Assert.That(remaining["bar"], Is.EqualTo("kept"));
        });
    }

    [Test]
    public void LimitLog_PrintsValueName()
    {
        using var logging = new TestUtilities.Logging(LogEventLevel.Debug);
        var userFamily = CreateFamilyDefinition();
        var agentFamily = CreateFamilyDefinition(AttributeClassification.AgentAttributes);
        for (var i = 1; i <= 64; i++)
        {
            _attribValues.TrySetValue(userFamily, Resolve($"user{i}"), i);
        }

        _attribValues.TrySetValue(userFamily, Resolve("user65"), 65);
        for (var i = 1; i <= 191; i++)
        {
            _attribValues.TrySetValue(agentFamily, Resolve($"agent{i}", AttributeClassification.AgentAttributes), i);
        }

        _attribValues.TrySetValue(agentFamily, Resolve("agent192", AttributeClassification.AgentAttributes), 192);

        Assert.Multiple(() =>
        {
            Assert.That(_attribValues.Count, Is.EqualTo(255));
            Assert.That(logging.HasMessageThatContains("User Attribute 'user65' was not recorded - A max of 64 User Attributes"), Is.True);
            Assert.That(logging.HasMessageThatContains("AgentAttributes Attribute 'agent192' was not recorded - A max of 255 attributes"), Is.True);
        });
    }

    [Test]
    public void ImmutableCollection_FamilySet_ThrowsInDebug()
    {
        var family = CreateFamilyDefinition();
        _attribValues.MakeImmutable();

#if DEBUG
        Assert.Throws<Exception>(() => _attribValues.TrySetValue(family, Resolve("foo"), "one"));
#else
        Assert.That(_attribValues.TrySetValue(family, Resolve("foo"), "one"), Is.False);
#endif
        Assert.That(_attribValues.Count, Is.EqualTo(0));
    }

    [Test]
    public void CopyIntoDestination_FamilyValues_UseKeyForLimits()
    {
        var family = CreateFamilyDefinition();
        var values = Enumerable.Range(1, 70)
            .Select(i => (IAttributeValue)new AttributeValue(family, $"name{i}", AttributeDestinations.TransactionEvent) { Value = i })
            .ToList();
        var destination = new AttributeValueCollection(AttributeDestinations.TransactionEvent);

        destination.AddRange(values);
        destination.AddRange(new List<IAttributeValue> { new AttributeValue(family, "name1", AttributeDestinations.TransactionEvent) { Value = 1000 } });

        var copied = destination.GetAttributeValuesDic(AttributeClassification.UserAttributes);
        Assert.Multiple(() =>
        {
            Assert.That(copied, Has.Count.EqualTo(64));
            Assert.That(copied.ContainsKey("name65"), Is.False);
            Assert.That(copied["name1"], Is.EqualTo(1000));
            Assert.That(destination.Count, Is.EqualTo(64));
        });
    }

    [Test]
    public void SpanCollection_AtTotalLimit_StaticOverwrite_Accepted()
    {
        var filter = new AttributeFilter(new AttributeFilter.Settings());
        var definitions = Enumerable.Range(1, 255)
            .Select(i => AttributeDefinitionBuilder.CreateLong($"agent{i}", AttributeClassification.AgentAttributes).AppliesTo(AttributeDestinations.All).Build(filter))
            .ToList();
        var spanCollection = new SpanAttributeValueCollection();
        foreach (var definition in definitions)
        {
            definition.TrySetValue(spanCollection, 1);
        }

        Assert.DoesNotThrow(() => definitions[0].TrySetValue(spanCollection, 2));

        var values = spanCollection.GetAttributeValuesDic(AttributeClassification.AgentAttributes);
        Assert.Multiple(() =>
        {
            Assert.That(spanCollection.Count, Is.EqualTo(255));
            Assert.That(values["agent1"], Is.EqualTo(2L));
        });
    }

    #endregion
}