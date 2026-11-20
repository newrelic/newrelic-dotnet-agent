// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Linq;
using NewRelic.Testing.Assertions;
using NUnit.Framework;
using Serilog.Events;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.Attributes.Tests;

[TestFixture]
public class DynamicAttributeDefinitionTests
{
    private const string FamilyLabel = "custom.*";

    private IAttributeValueCollection _collection;

    [SetUp]
    public void SetUp()
    {
        _collection = Mock.Create<IAttributeValueCollection>();
        Mock.Arrange(() => _collection.IsImmutable).Returns(false);
        Mock.Arrange(() => _collection.TargetModelTypes).Returns(new[] { AttributeDestinations.TransactionEvent });
    }

    private static ResolvedAttributeName Resolved(string name, AttributeDestinations destinations = AttributeDestinations.TransactionEvent, AttributeClassification classification = AttributeClassification.UserAttributes)
    {
        return new ResolvedAttributeName(name, classification, destinations);
    }

    private static DynamicAttributeDefinition<object, object> Create(
        ResolvedAttributeName resolved,
        AttributeClassification classification = AttributeClassification.UserAttributes,
        Func<object, object> convert = null,
        Func<object, object> postProcess = null)
    {
        return new DynamicAttributeDefinition<object, object>(FamilyLabel, classification, convert ?? AttributeDefinitionBuilder.GenericConverter, postProcess, _ => resolved);
    }

    private void AssertNotSet()
    {
        Mock.Assert(() => _collection.TrySetValue(Arg.IsAny<AttributeDefinition>(), Arg.IsAny<ResolvedAttributeName>(), Arg.IsAny<object>()), Occurs.Never());
    }

    [Test]
    public void TrySetValue_InvalidName_ReturnsFalse_NoSet()
    {
        var def = Create(Resolved(""));

        Assert.That(def.TrySetValue(_collection, "foo", "value"), Is.False);
        AssertNotSet();
    }

    [Test]
    public void TrySetValue_ImmutableCollection_ReturnsFalse_NoSet()
    {
        Mock.Arrange(() => _collection.IsImmutable).Returns(true);
        var def = Create(Resolved("foo"));

        Assert.That(def.TrySetValue(_collection, "foo", "value"), Is.False);
        AssertNotSet();
    }

    [Test]
    public void TrySetValue_NotAvailableForTargets_ReturnsFalse_NoSet()
    {
        var def = Create(Resolved("foo", AttributeDestinations.SpanEvent));

        Assert.That(def.TrySetValue(_collection, "foo", "value"), Is.False);
        AssertNotSet();
    }

    [Test]
    public void TrySetValue_NullValue_LogsAndReturnsFalse()
    {
        using var logging = new TestUtilities.Logging(LogEventLevel.Debug);
        var def = Create(Resolved("foo"));

        Assert.Multiple(() =>
        {
            Assert.That(def.TrySetValue(_collection, "foo", null), Is.False);
            Assert.That(logging.DebugCount, Is.EqualTo(1));
            Assert.That(logging.HasMessageThatContains("TransactionEvent UserAttributes Attribute 'foo' was not recorded - value was null"), Is.True);
        });
        AssertNotSet();
    }

    [Test]
    public void TrySetValue_NullValue_AgentClassification_NoLog()
    {
        using var logging = new TestUtilities.Logging(LogEventLevel.Debug);
        var def = Create(Resolved("foo", classification: AttributeClassification.AgentAttributes), AttributeClassification.AgentAttributes);

        Assert.Multiple(() =>
        {
            Assert.That(def.TrySetValue(_collection, "foo", null), Is.False);
            Assert.That(logging.DebugCount, Is.EqualTo(0));
        });
        AssertNotSet();
    }

    [Test]
    public void TrySetValue_ConvertReturnsNull_LogsAndReturnsFalse()
    {
        using var logging = new TestUtilities.Logging(LogEventLevel.Debug);
        var def = Create(Resolved("foo"), convert: _ => null);

        Assert.Multiple(() =>
        {
            Assert.That(def.TrySetValue(_collection, "foo", "value"), Is.False);
            Assert.That(logging.HasMessageThatContains("Attribute 'foo' was not recorded - value was null"), Is.True);
        });
        AssertNotSet();
    }

    [Test]
    public void TrySetValue_PostProcessReturnsNull_LogsAndReturnsFalse()
    {
        using var logging = new TestUtilities.Logging(LogEventLevel.Debug);
        var def = Create(Resolved("foo"), postProcess: _ => null);

        Assert.Multiple(() =>
        {
            Assert.That(def.TrySetValue(_collection, "foo", "value"), Is.False);
            Assert.That(logging.HasMessageThatContains("Attribute 'foo' was not recorded - value was null"), Is.True);
        });
        AssertNotSet();
    }

    [Test]
    public void TrySetValue_Success_PassesConvertedValueAndResolvedName()
    {
        var resolved = Resolved("foo");
        var def = Create(resolved);
        AttributeDefinition capturedDef = null;
        ResolvedAttributeName capturedResolved = null;
        object capturedValue = null;
        Mock.Arrange(() => _collection.TrySetValue(Arg.IsAny<AttributeDefinition>(), Arg.IsAny<ResolvedAttributeName>(), Arg.IsAny<object>()))
            .Returns((AttributeDefinition d, ResolvedAttributeName r, object v) =>
            {
                capturedDef = d;
                capturedResolved = r;
                capturedValue = v;
                return true;
            });

        var result = def.TrySetValue(_collection, "foo", TimeSpan.FromSeconds(2));

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.True);
            Assert.That(capturedDef, Is.SameAs(def));
            Assert.That(capturedResolved, Is.SameAs(resolved));
            Assert.That(capturedValue, Is.EqualTo(2.0));
        });
    }

    [Test]
    public void TrySetValue_Success_AppliesPostProcessing()
    {
        var def = Create(Resolved("foo"), postProcess: v => v + "!");
        object capturedValue = null;
        Mock.Arrange(() => _collection.TrySetValue(Arg.IsAny<AttributeDefinition>(), Arg.IsAny<ResolvedAttributeName>(), Arg.IsAny<object>()))
            .Returns((AttributeDefinition d, ResolvedAttributeName r, object v) =>
            {
                capturedValue = v;
                return false;
            });

        var result = def.TrySetValue(_collection, "foo", "value");

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.False);
            Assert.That(capturedValue, Is.EqualTo("value!"));
        });
    }

    [Test]
    public void TrySetValueUnchecked_SetsWithNoChecks()
    {
        using var logging = new TestUtilities.Logging(LogEventLevel.Debug);
        Mock.Arrange(() => _collection.IsImmutable).Returns(true);
        var resolved = Resolved("", AttributeDestinations.SpanEvent);
        var def = Create(resolved);
        var span = TimeSpan.FromSeconds(2);
        AttributeDefinition capturedDef = null;
        ResolvedAttributeName capturedResolved = null;
        object capturedValue = null;
        Mock.Arrange(() => _collection.TrySetValue(Arg.IsAny<AttributeDefinition>(), Arg.IsAny<ResolvedAttributeName>(), Arg.IsAny<object>()))
            .Returns((AttributeDefinition d, ResolvedAttributeName r, object v) =>
            {
                capturedDef = d;
                capturedResolved = r;
                capturedValue = v;
                return true;
            });

        var result = def.TrySetValueUnchecked(_collection, "foo", span);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.True);
            Assert.That(capturedDef, Is.SameAs(def));
            Assert.That(capturedResolved, Is.SameAs(resolved));
            Assert.That(capturedValue, Is.EqualTo(span));
            Assert.That(logging.HasMessageThatContains("not valid"), Is.False);
        });
    }

    [Test]
    public void TrySetValueUnchecked_ReturnsCollectionResult()
    {
        var def = Create(Resolved("foo"));
        Mock.Arrange(() => _collection.TrySetValue(Arg.IsAny<AttributeDefinition>(), Arg.IsAny<ResolvedAttributeName>(), Arg.IsAny<object>())).Returns(false);

        Assert.That(def.TrySetValueUnchecked(_collection, "foo", "value"), Is.False);
    }

    [Test]
    public void Resolve_ReturnsResolverResult()
    {
        string receivedName = null;
        var resolved = Resolved("foo");
        var def = new DynamicAttributeDefinition<object, object>(FamilyLabel, AttributeClassification.UserAttributes, AttributeDefinitionBuilder.GenericConverter, null, n =>
        {
            receivedName = n;
            return resolved;
        });

        Assert.Multiple(() =>
        {
            Assert.That(def.Resolve("raw"), Is.SameAs(resolved));
            Assert.That(receivedName, Is.EqualTo("raw"));
        });
    }

    private static AttributeFilter NoRulesFilter()
    {
        return new AttributeFilter(new AttributeFilter.Settings());
    }

    [Test]
    public void Resolve_SameName_ReturnsSameInstance()
    {
        var def = new AttributeDefinitions(NoRulesFilter()).TransactionCustomAttributes;

        var first = def.Resolve("foo");

        Assert.That(def.Resolve("foo"), Is.SameAs(first));
    }

    [Test]
    public void CachedNameCount_CountsDistinctNames()
    {
        var def = new AttributeDefinitions(NoRulesFilter()).TransactionCustomAttributes;
        Assert.That(def.CachedNameCount, Is.EqualTo(0));

        def.Resolve("foo");
        def.Resolve("foo");
        def.Resolve("bar");

        Assert.That(def.CachedNameCount, Is.EqualTo(2));
    }

    [Test]
    public void Resolve_255CharName_IsCached()
    {
        var def = new AttributeDefinitions(NoRulesFilter()).TransactionCustomAttributes;
        var name = new string('a', DynamicAttributeDefinition<object, object>.MaxCachedNameLength);

        var first = def.Resolve(name);

        Assert.Multiple(() =>
        {
            Assert.That(def.CachedNameCount, Is.EqualTo(1));
            Assert.That(def.Resolve(name), Is.SameAs(first));
        });
    }

    [Test]
    public void Resolve_256CharName_IsNotCached_ResultUnchanged()
    {
        var def = new AttributeDefinitions(NoRulesFilter()).TransactionCustomAttributes;
        var name = new string('a', DynamicAttributeDefinition<object, object>.MaxCachedNameLength + 1);

        var first = def.Resolve(name);
        var second = def.Resolve(name);
        var checkedCollection = new AttributeValueCollection(AttributeValueCollection.AllTargetModelTypes);
        var uncheckedCollection = new AttributeValueCollection(AttributeValueCollection.AllTargetModelTypes);
        var checkedResult = def.TrySetValue(checkedCollection, name, "value");
        var uncheckedResult = def.TrySetValueUnchecked(uncheckedCollection, name, "value");

        Assert.Multiple(() =>
        {
            Assert.That(def.CachedNameCount, Is.EqualTo(0));
            Assert.That(second, Is.Not.SameAs(first));
            Assert.That(second.Name, Is.EqualTo(first.Name));
            Assert.That(second.Destinations, Is.EqualTo(first.Destinations));
            Assert.That(checkedResult, Is.False);
            Assert.That(checkedCollection.GetAttributeValues(AttributeClassification.UserAttributes), Is.Empty);
            Assert.That(uncheckedResult, Is.True);
            Assert.That(uncheckedCollection.GetAttributeValues(AttributeClassification.UserAttributes).Select(x => x.Name), Is.EqualTo(new[] { name }));
        });
    }

    [Test]
    public void Resolve_NullName_StillThrowsArgumentNullException()
    {
        var def = new AttributeDefinitions(NoRulesFilter()).TransactionCustomAttributes;

        Assert.Throws<ArgumentNullException>(() => def.Resolve(null));
    }

    [Test]
    public void MemoryBound_100kRequestParameterNames()
    {
        var def = new AttributeDefinitions(IncludeAllFilter()).RequestParameterAttributes;

        for (var i = 0; i < 100000; i++)
        {
            def.Resolve($"p{i}");
        }

        Assert.That(def.CachedNameCount, Is.LessThanOrEqualTo(DynamicAttributeDefinition<string, string>.NameCacheCapacity));
    }

#if NET
    [Test]
    public void Resolve_Hit_AllocatesZeroBytes()
    {
        var def = new AttributeDefinitions(IncludeAllFilter()).RequestParameterAttributes;
        def.Resolve("foo");

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1000; i++)
        {
            def.Resolve("foo");
        }
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.That(allocated, Is.EqualTo(0));
    }

    [Test]
    public void FamilySet_KnownName_AllocatesSameAsStaticSet()
    {
        var family = new AttributeDefinitions(NoRulesFilter()).SpanCustomAttributes;
        var staticDefinition = AttributeDefinitionBuilder.Create<object>("foo", AttributeClassification.UserAttributes)
            .WithConvert(AttributeDefinitionBuilder.GenericConverter)
            .AppliesTo(AttributeDestinations.SpanEvent)
            .Build(NoRulesFilter());
        object value = 1L;
        var familyCollection = new AttributeValueCollection(AttributeValueCollection.AllTargetModelTypes);
        var staticCollection = new AttributeValueCollection(AttributeValueCollection.AllTargetModelTypes);

        long Measure(Action set)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 1000; i++)
            {
                set();
            }
            return GC.GetAllocatedBytesForCurrentThread() - before;
        }

        void FamilySet() => family.TrySetValue(familyCollection, "foo", value);
        void StaticSet() => staticDefinition.TrySetValue(staticCollection, value);

        // The first rounds absorb one-time JIT and growth allocations.
        for (var round = 0; round < 3; round++)
        {
            Measure(FamilySet);
            Measure(StaticSet);
        }

        Assert.That(Measure(FamilySet), Is.EqualTo(Measure(StaticSet)));
    }
#endif

    [Test]
    public void InvalidName_LogsOncePerCachedName()
    {
        using var logging = new TestUtilities.Logging(LogEventLevel.Debug);
        var def = new AttributeDefinitions(NoRulesFilter()).SpanCustomAttributes;
        var collection = new AttributeValueCollection(AttributeValueCollection.AllTargetModelTypes);

        def.TrySetValue(collection, " ", 1L);
        def.TrySetValue(collection, " ", 1L);
        var afterCachedName = logging.DebugCount;

        var longName = new string('a', DynamicAttributeDefinition<object, object>.MaxCachedNameLength + 1);
        def.TrySetValue(collection, longName, 1L);
        def.TrySetValue(collection, longName, 1L);

        Assert.Multiple(() =>
        {
            Assert.That(afterCachedName, Is.EqualTo(1));
            Assert.That(logging.DebugCount - afterCachedName, Is.EqualTo(2));
        });
    }

    [Test]
    public void ConfigurationRebuild_StartsWithEmptyCaches()
    {
        var filter = IncludeAllFilter();
        var oldDefinitions = new AttributeDefinitions(filter);
        oldDefinitions.RequestParameterAttributes.Resolve("foo");

        var newDefinitions = new AttributeDefinitions(filter);

        Assert.Multiple(() =>
        {
            Assert.That(oldDefinitions.RequestParameterAttributes.CachedNameCount, Is.EqualTo(1));
            Assert.That(newDefinitions.RequestParameterAttributes.CachedNameCount, Is.EqualTo(0));
            Assert.That(newDefinitions.RequestParameterAttributes.Guid, Is.Not.EqualTo(oldDefinitions.RequestParameterAttributes.Guid));
        });
    }

    [Test]
    public void TrySetValue_NullName_Throws()
    {
        var def = Create(Resolved("foo"));

        Assert.Throws<ArgumentNullException>(() => def.TrySetValue(_collection, null, "value"));
    }

    [Test]
    public void TrySetValueUnchecked_NullName_Throws()
    {
        var def = Create(Resolved("foo"));

        Assert.Throws<ArgumentNullException>(() => def.TrySetValueUnchecked(_collection, null, "value"));
    }

    [Test]
    public void Constructor_SetsLabelAndClassification()
    {
        var def = Create(Resolved("foo"), AttributeClassification.AgentAttributes);

        Assert.Multiple(() =>
        {
            Assert.That(def.Name, Is.EqualTo(FamilyLabel));
            Assert.That(def.Classification, Is.EqualTo(AttributeClassification.AgentAttributes));
        });
    }

    [Test]
    public void BaseIsAvailableForAny_IsFalse()
    {
        var def = Create(Resolved("foo"));

        Assert.That(def.IsAvailableForAny(AttributeDestinations.TransactionEvent), Is.False);
    }

    private static AttributeFilter IncludeAllFilter()
    {
        return new AttributeFilter(new AttributeFilter.Settings { Includes = new[] { "*" } });
    }

    private static (AttributeDefinition Definition, Func<string, ResolvedAttributeName> Resolve) Family(IAttributeDefinitions defs, string family)
    {
        switch (family)
        {
            case nameof(IAttributeDefinitions.TransactionCustomAttributes):
                return (defs.TransactionCustomAttributes, defs.TransactionCustomAttributes.Resolve);
            case nameof(IAttributeDefinitions.SpanCustomAttributes):
                return (defs.SpanCustomAttributes, defs.SpanCustomAttributes.Resolve);
            case nameof(IAttributeDefinitions.ErrorCustomAttributes):
                return (defs.ErrorCustomAttributes, defs.ErrorCustomAttributes.Resolve);
            case nameof(IAttributeDefinitions.CustomEventCustomAttributes):
                return (defs.CustomEventCustomAttributes, defs.CustomEventCustomAttributes.Resolve);
            case nameof(IAttributeDefinitions.RequestParameterAttributes):
                return (defs.RequestParameterAttributes, defs.RequestParameterAttributes.Resolve);
            case nameof(IAttributeDefinitions.RequestHeaderAttributes):
                return (defs.RequestHeaderAttributes, defs.RequestHeaderAttributes.Resolve);
            default:
                throw new ArgumentException(family);
        }
    }

    [TestCase(nameof(IAttributeDefinitions.TransactionCustomAttributes), "custom.transaction.*", AttributeClassification.UserAttributes, "foo", AttributeDestinations.All)]
    [TestCase(nameof(IAttributeDefinitions.SpanCustomAttributes), "custom.span.*", AttributeClassification.UserAttributes, "foo", AttributeDestinations.SpanEvent)]
    [TestCase(nameof(IAttributeDefinitions.ErrorCustomAttributes), "custom.error.*", AttributeClassification.UserAttributes, "foo", AttributeDestinations.ErrorEvent | AttributeDestinations.ErrorTrace)]
    [TestCase(nameof(IAttributeDefinitions.CustomEventCustomAttributes), "custom.event.*", AttributeClassification.UserAttributes, "foo", AttributeDestinations.CustomEvent)]
    [TestCase(nameof(IAttributeDefinitions.RequestParameterAttributes), "request.parameters.*", AttributeClassification.AgentAttributes, "request.parameters.foo", AttributeDestinations.TransactionTrace | AttributeDestinations.TransactionEvent | AttributeDestinations.ErrorTrace | AttributeDestinations.ErrorEvent | AttributeDestinations.SpanEvent)]
    [TestCase(nameof(IAttributeDefinitions.RequestHeaderAttributes), "request.headers.*", AttributeClassification.AgentAttributes, "request.headers.foo", AttributeDestinations.TransactionTrace | AttributeDestinations.TransactionEvent | AttributeDestinations.ErrorTrace | AttributeDestinations.ErrorEvent | AttributeDestinations.SpanEvent)]
    public void Families_HaveTodaysRules(string family, string label, AttributeClassification classification, string expectedName, AttributeDestinations expectedDestinations)
    {
        using var logging = new TestUtilities.Logging(LogEventLevel.Debug);
        var (definition, resolve) = Family(new AttributeDefinitions(IncludeAllFilter()), family);

        var resolved = resolve("foo");
        var longNameIsValid = resolve(new string('a', 256)).IsNameValid;

        Assert.Multiple(() =>
        {
            Assert.That(definition.Name, Is.EqualTo(label));
            Assert.That(definition.Classification, Is.EqualTo(classification));
            Assert.That(resolved.Name, Is.EqualTo(expectedName));
            Assert.That(resolved.Destinations, Is.EqualTo(expectedDestinations));
            Assert.That(longNameIsValid, Is.False);
            Assert.That(logging.HasMessageThatContains($"{classification} Attribute definition is not valid"), Is.True);
        });
    }

    [TestCase(nameof(IAttributeDefinitions.TransactionCustomAttributes))]
    [TestCase(nameof(IAttributeDefinitions.SpanCustomAttributes))]
    [TestCase(nameof(IAttributeDefinitions.ErrorCustomAttributes))]
    [TestCase(nameof(IAttributeDefinitions.CustomEventCustomAttributes))]
    [TestCase(nameof(IAttributeDefinitions.RequestParameterAttributes))]
    [TestCase(nameof(IAttributeDefinitions.RequestHeaderAttributes))]
    public void Families_AreSingletonsPerAttributeDefinitions(string family)
    {
        var defs = new AttributeDefinitions(IncludeAllFilter());
        var other = new AttributeDefinitions(IncludeAllFilter());

        var firstRead = Family(defs, family).Definition;
        var secondRead = Family(defs, family).Definition;

        Assert.Multiple(() =>
        {
            Assert.That(secondRead, Is.SameAs(firstRead));
            Assert.That(Family(other, family).Definition, Is.Not.SameAs(firstRead));
        });
    }

    private static DynamicAttributeDefinition<object, object> CustomFamily(string family)
    {
        var defs = new AttributeDefinitions(new AttributeFilter(new AttributeFilter.Settings()));

        switch (family)
        {
            case nameof(IAttributeDefinitions.TransactionCustomAttributes):
                return defs.TransactionCustomAttributes;
            case nameof(IAttributeDefinitions.SpanCustomAttributes):
                return defs.SpanCustomAttributes;
            case nameof(IAttributeDefinitions.ErrorCustomAttributes):
                return defs.ErrorCustomAttributes;
            case nameof(IAttributeDefinitions.CustomEventCustomAttributes):
                return defs.CustomEventCustomAttributes;
            default:
                throw new ArgumentException(family);
        }
    }

    [TestCase(nameof(IAttributeDefinitions.TransactionCustomAttributes))]
    [TestCase(nameof(IAttributeDefinitions.SpanCustomAttributes))]
    [TestCase(nameof(IAttributeDefinitions.ErrorCustomAttributes))]
    [TestCase(nameof(IAttributeDefinitions.CustomEventCustomAttributes))]
    public void CustomFamilies_ConvertThroughGenericConverter(string family)
    {
        var attribVals = new AttributeValueCollection(AttributeValueCollection.AllTargetModelTypes);
        var definition = CustomFamily(family);

        definition.TrySetValue(attribVals, "foo", TimeSpan.FromSeconds(2));
        definition.TrySetValue(attribVals, "bar", 5);

        var values = attribVals.GetAttributeValues(AttributeClassification.UserAttributes).ToDictionary(x => x.Name, x => x.Value);

        Assert.Multiple(() =>
        {
            Assert.That(values["foo"], Is.EqualTo(2.0d));
            Assert.That(values["bar"], Is.EqualTo(5L));
        });
    }

    [Test]
    public void RequestFamilies_TruncateTo255Bytes()
    {
        var defs = new AttributeDefinitions(IncludeAllFilter());
        var attribVals = new AttributeValueCollection(AttributeValueCollection.AllTargetModelTypes);

        defs.RequestParameterAttributes.TrySetValue(attribVals, "foo", new string('a', 300));
        defs.RequestHeaderAttributes.TrySetValue(attribVals, "bar", new string('b', 300));

        var values = attribVals.GetAttributeValues(AttributeClassification.AgentAttributes).ToDictionary(x => x.Name, x => (string)x.Value);

        Assert.Multiple(() =>
        {
            Assert.That(values["request.parameters.foo"], Has.Length.EqualTo(255));
            Assert.That(values["request.headers.bar"], Has.Length.EqualTo(255));
        });
    }

    [TestCase("stringValue", "stringValue")]
    [TestCase(true, true)]
    [TestCase((sbyte)1, 1L)]
    [TestCase((byte)2, 2L)]
    [TestCase((short)3, 3L)]
    [TestCase((ushort)4, 4L)]
    [TestCase(/*(int)*/5, 5L)]
    [TestCase((uint)6, 6L)]
    [TestCase((long)7, 7L)]
    [TestCase((ulong)8, 8L)]
    [TestCase((float)1.0, 1D)]
    [TestCase(/*(double)*/2.0, 2D)]
    [TestCase(0.2D, 0.2D)]
    [TestCase(0.2F, 0.2F)]
    public void Attributes_with_valid_type_are_valid_attributes(object attributeValue, object expectedResult)
    {
        TestValue(attributeValue, expectedResult);
    }

    [Test]
    public void Attributes_with_decimal_type_are_valid_attributes()
    {
        TestValue(0.2M, 0.2M);
    }

    [Test]
    public void Attributes_with_DateTime_type_are_valid_attributes()
    {
        var testValue = DateTime.Now;
        var expectedValue = testValue.ToString("o");

        TestValue(testValue, expectedValue);
    }

    [Test]
    public void Attributes_with_TimeSpan_type_are_valid_attributes()
    {
        var testValue = TimeSpan.FromMilliseconds(1234);
        var expectedValue = 1.234d;

        TestValue(testValue, expectedValue);
    }

    [Test]
    public void Attributes_with_null_values_are_invalid_attributes()
    {
        var attribDef = new AttributeDefinitions(new AttributeFilter(new AttributeFilter.Settings())).TransactionCustomAttributes;
        var attribVals = new AttributeValueCollection(AttributeValueCollection.AllTargetModelTypes);

        var trySetResult = attribDef.TrySetValue(attribVals, "test", null);

        var actualAttribVal = attribVals.GetAttributeValues(AttributeClassification.UserAttributes)
            .FirstOrDefault(x => x.Name == "test");

        NrAssert.Multiple(
            () => Assert.That(trySetResult, Is.False),
            () => Assert.That(actualAttribVal, Is.Null)
        );
    }

    [Test]
    public void Attributes_with_empty_values_are_valid_attributes()
    {
        var attribDef = new AttributeDefinitions(new AttributeFilter(new AttributeFilter.Settings())).TransactionCustomAttributes;
        var attribVals = new AttributeValueCollection(AttributeValueCollection.AllTargetModelTypes);

        var trySetResult = attribDef.TrySetValue(attribVals, "test", string.Empty);

        var actualAttribVal = attribVals.GetAttributeValues(AttributeClassification.UserAttributes)
            .FirstOrDefault(x => x.Name == "test");

        NrAssert.Multiple(
            () => Assert.That(trySetResult, Is.True),
            () => Assert.That(actualAttribVal.Value, Is.EqualTo(string.Empty))
        );
    }

    [Test]
    public void Attributes_with_blank_values_are_valid_attributes()
    {
        var attribDef = new AttributeDefinitions(new AttributeFilter(new AttributeFilter.Settings())).TransactionCustomAttributes;
        var attribVals = new AttributeValueCollection(AttributeValueCollection.AllTargetModelTypes);

        var trySetResult = attribDef.TrySetValue(attribVals, "test", " ");

        var actualAttribVal = attribVals.GetAttributeValues(AttributeClassification.UserAttributes)
            .FirstOrDefault(x => x.Name == "test");

        NrAssert.Multiple(
            () => Assert.That(trySetResult, Is.True),
            () => Assert.That(actualAttribVal.Value, Is.EqualTo(" "))
        );
    }

    [Test]
    public void Attributes_key_size()
    {
        var attribDef = new AttributeDefinitions(new AttributeFilter(new AttributeFilter.Settings())).TransactionCustomAttributes;
        var attribVals = new AttributeValueCollection(AttributeValueCollection.AllTargetModelTypes);

        var testKeys = new[]
        {
            new string('x', 255),
            new string('a', 256),
            string.Empty,
            " "
        };

        var testResults = new bool[testKeys.Length];

        for (var i = 0; i < testKeys.Length; i++)
        {
            testResults[i] = attribDef.Resolve(testKeys[i]).IsNameValid;

            attribDef.TrySetValue(attribVals, testKeys[i], 9);
        }

        NrAssert.Multiple(
            () => Assert.That(testResults[0], Is.True),
            () => Assert.That(testResults[1], Is.False),
            () => Assert.That(testResults[2], Is.False),
            () => Assert.That(testResults[3], Is.False),
            () => Assert.Throws<ArgumentNullException>(() => attribDef.Resolve(null)),
            () => Assert.That(attribVals.GetAttributeValues(AttributeClassification.UserAttributes).Count(), Is.EqualTo(1))
        );
    }

    private static void TestValue(object attributeValue, object expectedResult)
    {
        var attribDef = new AttributeDefinitions(new AttributeFilter(new AttributeFilter.Settings())).TransactionCustomAttributes;
        var attribVals = new AttributeValueCollection(AttributeValueCollection.AllTargetModelTypes);

        attribDef.TrySetValue(attribVals, "test", attributeValue);

        var actualAttribVal = attribVals.GetAttributeValues(AttributeClassification.UserAttributes)
            .FirstOrDefault(x => x.Name == "test");

        NrAssert.Multiple(
            () => Assert.That(actualAttribVal, Is.Not.Null),
            () => Assert.That(actualAttribVal.Value, Is.EqualTo(expectedResult))
        );
    }
}
