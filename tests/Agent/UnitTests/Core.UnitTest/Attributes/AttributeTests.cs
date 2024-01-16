// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NUnit.Framework.Internal;

namespace NewRelic.Agent.Core.Attributes.Tests
{
    [TestFixture]
    public class AttributeTests
    {
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
        public void Attributes_with_valid_type_are_valid_attributes(object attributeValue, object expectedResult)
        {
            TestValue(attributeValue, expectedResult);
        }


        private void TestValue(object attributeValue, object expectedResult)
        {
            var filter = new AttributeFilter(new AttributeFilter.Settings());

            var attribDef = AttributeDefinitionBuilder
                .CreateCustomAttribute("test", AttributeDestinations.All)
                .Build(filter);

            var attribVals = new AttributeValueCollection(AttributeValueCollection.AllTargetModelTypes);

            attribDef.TrySetValue(attribVals, attributeValue);

            var actualAttribVal = attribVals.GetAttributeValues(AttributeClassification.UserAttributes)
                .FirstOrDefault(x => x.AttributeDefinition == attribDef);


            NrAssert.Multiple(
                () => ClassicAssert.IsNotNull(actualAttribVal),
                () => ClassicAssert.AreEqual(expectedResult, actualAttribVal.Value)
            );
        }

        [Test]
        public void Attributes_with_decimal_type_are_valid_attributes()
        {
            TestValue(1.0m, 1D);
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
            var filter = new AttributeFilter(new AttributeFilter.Settings());

            var attribDef = AttributeDefinitionBuilder
                .CreateCustomAttribute("test", AttributeDestinations.All)
                .Build(filter);

            var attribVals = new AttributeValueCollection(AttributeValueCollection.AllTargetModelTypes);

            var trySetResult = attribDef.TrySetValue(attribVals, null);

            var actualAttribVal = attribVals.GetAttributeValues(AttributeClassification.UserAttributes)
               .FirstOrDefault(x => x.AttributeDefinition == attribDef);


            NrAssert.Multiple(
                () => ClassicAssert.IsFalse(trySetResult),
                () => ClassicAssert.IsNull(actualAttribVal)
            );
        }

        [Test]
        public void Attributes_with_empty_values_are_valid_attributes()
        {
            var filter = new AttributeFilter(new AttributeFilter.Settings());

            var attribDef = AttributeDefinitionBuilder
                .CreateCustomAttribute("test", AttributeDestinations.All)
                .Build(filter);

            var attribVals = new AttributeValueCollection(AttributeValueCollection.AllTargetModelTypes);

            var trySetResult = attribDef.TrySetValue(attribVals, string.Empty);

            var actualAttribVal = attribVals.GetAttributeValues(AttributeClassification.UserAttributes)
               .FirstOrDefault(x => x.AttributeDefinition == attribDef);

            NrAssert.Multiple(
                () => ClassicAssert.IsTrue(trySetResult),
                () => ClassicAssert.AreEqual(string.Empty, actualAttribVal.Value)
            );
        }

        [Test]
        public void Attributes_with_blank_values_are_valid_attributes()
        {
            var filter = new AttributeFilter(new AttributeFilter.Settings());

            var attribDef = AttributeDefinitionBuilder
                .CreateCustomAttribute("test", AttributeDestinations.All)
                .Build(filter);

            var attribVals = new AttributeValueCollection(AttributeValueCollection.AllTargetModelTypes);

            var trySetResult = attribDef.TrySetValue(attribVals, " ");

            var actualAttribVal = attribVals.GetAttributeValues(AttributeClassification.UserAttributes)
               .FirstOrDefault(x => x.AttributeDefinition == attribDef);


            NrAssert.Multiple(
                () => ClassicAssert.IsTrue(trySetResult),
                () => ClassicAssert.AreEqual(" ", actualAttribVal.Value)
            );
        }

        [Test]
        public void Attributes_key_size()
        {
            var filter = new AttributeFilter(new AttributeFilter.Settings());
            var attribVals = new AttributeValueCollection(AttributeValueCollection.AllTargetModelTypes);

            var testKeys = new string[]
                {
                    new string('x', 255),
                    new string('a', 256),
                    string.Empty,
                    " ",
                    null as string
                };

            var testResults = new bool[testKeys.Length];


            for(var i = 0; i < testKeys.Length; i++)
            {
                var attribDef = AttributeDefinitionBuilder
                   .CreateCustomAttribute(testKeys[i], AttributeDestinations.All)
                   .Build(filter);

                testResults[i] = attribDef.IsDefinitionValid;

                attribDef.TrySetValue(attribVals, 9);
            }

            NrAssert.Multiple(
                () => ClassicAssert.IsTrue(testResults[0]),
                () => ClassicAssert.IsFalse(testResults[1]),
                () => ClassicAssert.IsFalse(testResults[2]),
                () => ClassicAssert.IsFalse(testResults[3]),
                () => ClassicAssert.IsFalse(testResults[4]),
                () => ClassicAssert.AreEqual(1, attribVals.GetAttributeValues(AttributeClassification.UserAttributes).Count())
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
                () => ClassicAssert.IsTrue(attribDefTestResults[0]),
                () => ClassicAssert.IsTrue(attribDefTestResults[1]),
                () => ClassicAssert.IsTrue(attribDefTestResults[2]),
                () => ClassicAssert.AreEqual(3, values.Count()),
                () => ClassicAssert.AreEqual(255, values.ElementAt(0).Value.ToString().Length),
                () => ClassicAssert.AreEqual(1023, values.ElementAt(1).Value.ToString().Length),
                () => ClassicAssert.AreEqual(1023, values.ElementAt(2).Value.ToString().Length)
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
                () => ClassicAssert.IsTrue(attribDefTestResults[0]),
                () => ClassicAssert.IsTrue(attribDefTestResults[1]),
                () => ClassicAssert.IsTrue(attribDefTestResults[2]),
                () => ClassicAssert.AreEqual(3, values.Count()),
                () => ClassicAssert.AreEqual(255, values.ElementAt(0).Value.ToString().Length),
                () => ClassicAssert.AreEqual(255, values.ElementAt(1).Value.ToString().Length),
                () => ClassicAssert.AreEqual(255, values.ElementAt(2).Value.ToString().Length)
            );
        }
    }
}
