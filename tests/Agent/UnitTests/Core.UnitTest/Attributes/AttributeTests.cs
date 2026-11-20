// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Testing.Assertions;
using NUnit.Framework;

namespace NewRelic.Agent.Core.Attributes.Tests;

[TestFixture]
public class AttributeTests
{
    [Test]
    public void Attributes_with_null_lazy_getter_are_invalid_attributes()
    {
        var attribDef = AttributeDefinitionBuilder
            .Create<object, object>("test", AttributeClassification.UserAttributes)
            .AppliesTo(AttributeDestinations.All, true)
            .WithConvert(AttributeDefinitionBuilder.GenericConverter)
            .Build(new AttributeFilter(new AttributeFilter.Settings()));
        var attribVals = new AttributeValueCollection(AttributeValueCollection.AllTargetModelTypes);

        var trySetResult = attribDef.TrySetValue(attribVals, (Func<object>)null);

        NrAssert.Multiple(
            () => Assert.That(trySetResult, Is.False),
            () => Assert.That(attribVals.GetAttributeValues(AttributeClassification.UserAttributes), Is.Empty)
        );
    }

    [Test]
    public void Attributes_value_size_errors()
    {
        var filter = new AttributeFilter(new AttributeFilter.Settings());
        var attribVals = new AttributeValueCollection(AttributeValueCollection.AllTargetModelTypes);

        var attributes = new Dictionary<string, string>
        {
            { "short" ,new string('a', 255) },
            { "long", new string('b', 1023) },
            { "toolong",new string('c', 1024) }
        };

        var attribDefTestResults = new bool[attributes.Count];

        for (int i = 0; i < attributes.Count; i++)
        {
            var pair = attributes.ElementAt(i);
            var attribDef = AttributeDefinitionBuilder
                .CreateErrorMessage(pair.Key, AttributeClassification.AgentAttributes)
                .AppliesTo(AttributeDestinations.ErrorEvent, AttributeDestinations.ErrorTrace)
                .Build(filter);
            attribDefTestResults[i] = attribDef.IsDefinitionValid;
            attribDef.TrySetValue(attribVals, pair.Value);
        }

        var values = attribVals.GetAttributeValues(AttributeClassification.AgentAttributes);

        NrAssert.Multiple(
            () => Assert.That(attribDefTestResults[0], Is.True),
            () => Assert.That(attribDefTestResults[1], Is.True),
            () => Assert.That(attribDefTestResults[2], Is.True),
            () => Assert.That(values.Count(), Is.EqualTo(3)),
            () => Assert.That(values.ElementAt(0).Value.ToString(), Has.Length.EqualTo(255)),
            () => Assert.That(values.ElementAt(1).Value.ToString(), Has.Length.EqualTo(1023)),
            () => Assert.That(values.ElementAt(2).Value.ToString(), Has.Length.EqualTo(1023))
        );
    }

    [Test]
    public void Attributes_value_size_other_strings()
    {
        var filter = new AttributeFilter(new AttributeFilter.Settings());
        var attribVals = new AttributeValueCollection(AttributeValueCollection.AllTargetModelTypes);

        var attributes = new Dictionary<string, string>
        {
            { "short" ,new string('a', 255) },
            { "long", new string('b', 1023) },
            { "toolong",new string('c', 1024) }
        };

        var attribDefTestResults = new bool[attributes.Count];

        for (int i = 0; i < attributes.Count; i++)
        {
            var pair = attributes.ElementAt(i);
            var attribDef = AttributeDefinitionBuilder
                .CreateString(pair.Key, AttributeClassification.AgentAttributes)
                .AppliesTo(AttributeDestinations.All)
                .Build(filter);
            attribDefTestResults[i] = attribDef.IsDefinitionValid;
            attribDef.TrySetValue(attribVals, pair.Value);
        }

        var values = attribVals.GetAttributeValues(AttributeClassification.AgentAttributes);

        NrAssert.Multiple(
            () => Assert.That(attribDefTestResults[0], Is.True),
            () => Assert.That(attribDefTestResults[1], Is.True),
            () => Assert.That(attribDefTestResults[2], Is.True),
            () => Assert.That(values.Count(), Is.EqualTo(3)),
            () => Assert.That(values.ElementAt(0).Value.ToString(), Has.Length.EqualTo(255)),
            () => Assert.That(values.ElementAt(1).Value.ToString(), Has.Length.EqualTo(255)),
            () => Assert.That(values.ElementAt(2).Value.ToString(), Has.Length.EqualTo(255))
        );
    }
}
