// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Linq;
using NewRelic.Agent.Core.JsonConverters;
using Newtonsoft.Json;
using NUnit.Framework;

namespace NewRelic.Agent.Core.Utilities
{
    [TestFixture]
    public class ConvertJsonToStringArrayForCatTests
    {
        private string _wellformedJson = "[1,'single quoted string',true,\"double quoted string\",null, 4.32,5]";
        private string _wellformedJson_Booleans = "[true,false,true]";
        private string _wellformedJson_EmptyArray = "[]";
        private string _wellformedJson_MixedQuotes = "[\"this 'is' valid\",'so \"is\" this']";
        private string _wellformedJson_NotAnArray1 = "{}";
        private string _wellformedJson_NotAnArray2 = "{ color : 'red', size : 'large'}";
        private string _wellformedJson_EmbeddedArray = "[1,2,[3,4,5],6,7]";

        private string _malformedJson_ArrayNotClosed = "[1,2,3";
        private string _malformedJson_UnclosedQuote = "[1,'two,3,4,5]";
        private string _malformedJson_EmptyElement = "[1,,2,3]";
        private string _malformedJson_NonQuotedElements = "[1,2,tree,four,five,6]";
        private string _malformedJson_NotJson = "This is not JSON";

        [Test]
        public void Deserialize_NoErrors()
        {
            Assert.DoesNotThrow(() => CrossApplicationTracingJsonHelper.ConvertJsonToStringArrayForCat(_wellformedJson, 0, 7));
        }

        [Test]
        public void Deserialize_ResultArrayMatchesJsonArrayOrder()
        {
            var result = CrossApplicationTracingJsonHelper.ConvertJsonToStringArrayForCat(_wellformedJson, 0, 7);

            Assert.That(result, Has.Length.EqualTo(7));
            Assert.Multiple(() =>
            {
                Assert.That(result[0], Is.EqualTo("1"));
                Assert.That(result[1], Is.EqualTo("single quoted string"));
                Assert.That(result[2], Is.EqualTo("True"));
                Assert.That(result[3], Is.EqualTo("double quoted string"));
                Assert.That(result[4], Is.EqualTo(null));
                Assert.That(result[5], Is.EqualTo("4.32"));
                Assert.That(result[6], Is.EqualTo("5"));
            });
        }

        [Test]
        public void Deserialize_ArraySizeMatchesUpperBound()
        {
            var result = CrossApplicationTracingJsonHelper.ConvertJsonToStringArrayForCat(_wellformedJson, 0, 7);
            Assert.That(result, Has.Length.EqualTo(7));

            result = CrossApplicationTracingJsonHelper.ConvertJsonToStringArrayForCat(_wellformedJson, 0, 10);
            Assert.That(result, Has.Length.EqualTo(10));
        }

        private void Deserialize_EmptyJsonArray_ResultsInNotNull()
        {
            var result = CrossApplicationTracingJsonHelper.ConvertJsonToStringArrayForCat(_wellformedJson_EmptyArray, 0, 7);

            Assert.That(result, Is.Not.Null);
            Assert.That(result, Has.Length.EqualTo(7));
            Assert.That(result.All(x => x == null), Is.True);
        }

        [Test]
        public void Deserialize_OversizedJsonArray_ResultsInNull()
        {
            for (var i = 0; i < 7; i++)
            {
                var resultExpectedNull = CrossApplicationTracingJsonHelper.ConvertJsonToStringArrayForCat(_wellformedJson, 0, i);
                Assert.That(resultExpectedNull, Is.Null);
            }

            var resultExpectedNotNull = CrossApplicationTracingJsonHelper.ConvertJsonToStringArrayForCat(_wellformedJson, 0, 8);
            Assert.That(resultExpectedNotNull, Is.Not.Null);
        }

        [Test]
        public void Deserialize_UndersizedJsonArray_ResultsInNull()
        {
            var resultExpectedNull = CrossApplicationTracingJsonHelper.ConvertJsonToStringArrayForCat(_wellformedJson, 8, 100);
            Assert.That(resultExpectedNull, Is.Null);

            for (var i = 0; i <= 7; i++)
            {
                var resultExpectedNotNull = CrossApplicationTracingJsonHelper.ConvertJsonToStringArrayForCat(_wellformedJson, i, 100);
                Assert.That(resultExpectedNotNull, Is.Not.Null);
            }
        }

        [Test]
        public void Deserialize_WellFormedJson_MalformedJsonArray_ResultsInNull()
        {
            var resultExpectedNull = CrossApplicationTracingJsonHelper.ConvertJsonToStringArrayForCat(_malformedJson_ArrayNotClosed, 0, 100);
            Assert.That(resultExpectedNull, Is.Null);

            resultExpectedNull = CrossApplicationTracingJsonHelper.ConvertJsonToStringArrayForCat(_wellformedJson_NotAnArray1, 0, 100);
            Assert.That(resultExpectedNull, Is.Null);

            resultExpectedNull = CrossApplicationTracingJsonHelper.ConvertJsonToStringArrayForCat(_wellformedJson_NotAnArray2, 0, 100);
            Assert.That(resultExpectedNull, Is.Null);
        }

        [Test]
        public void Deserialize_WellFormedJson_BooleansMatchDotNetStringRepresentation()
        {
            var result = CrossApplicationTracingJsonHelper.ConvertJsonToStringArrayForCat(_wellformedJson_Booleans, 0, 3);
            Assert.Multiple(() =>
            {
                Assert.That(result[0], Is.EqualTo(true.ToString()));
                Assert.That(result[1], Is.EqualTo(false.ToString()));
                Assert.That(result[2], Is.EqualTo(true.ToString()));
            });
            Assert.Multiple(() =>
            {
                Assert.That(result[0], Is.Not.EqualTo("true"));
                Assert.That(result[1], Is.Not.EqualTo("false"));
            });
        }

        [Test]
        public void Deserialize_EmptyString_ResultsInNull()
        {
            var resultExpectedNull = CrossApplicationTracingJsonHelper.ConvertJsonToStringArrayForCat(string.Empty, 0, 100);
            Assert.That(resultExpectedNull, Is.Null);

            resultExpectedNull = CrossApplicationTracingJsonHelper.ConvertJsonToStringArrayForCat(" ", 0, 100);
            Assert.That(resultExpectedNull, Is.Null);
        }

        [Test]
        public void Deserialize_EmptyElement_ResultsInNull()
        {
            var resultExpectedNull = CrossApplicationTracingJsonHelper.ConvertJsonToStringArrayForCat(_malformedJson_EmptyElement, 0, 100);
            Assert.That(resultExpectedNull, Is.Null);
        }

        [Test]
        public void Deserialize_EmbeddedArray_ResultsInNull()
        {
            var resultExpectedNull = CrossApplicationTracingJsonHelper.ConvertJsonToStringArrayForCat(_wellformedJson_EmbeddedArray, 0, 100);
            Assert.That(resultExpectedNull, Is.Null);
        }

        [Test]
        public void Deserialize_MalformedJson_ThrowsJsonReaderException()
        {
            Assert.Throws(typeof(JsonReaderException), () => CrossApplicationTracingJsonHelper.ConvertJsonToStringArrayForCat(_malformedJson_UnclosedQuote, 0, 100));
            Assert.Throws(typeof(JsonReaderException), () => CrossApplicationTracingJsonHelper.ConvertJsonToStringArrayForCat(_malformedJson_NotJson, 0, 100));
            Assert.Throws(typeof(JsonReaderException), () => CrossApplicationTracingJsonHelper.ConvertJsonToStringArrayForCat(_malformedJson_NonQuotedElements, 0, 100));

        }

        [Test]
        public void Deserialize_HandlesSingleAndDoubleQuotedStrings()
        {
            var result = CrossApplicationTracingJsonHelper.ConvertJsonToStringArrayForCat(_wellformedJson_MixedQuotes, 0, 2);

            Assert.Multiple(() =>
            {
                Assert.That(result[0], Is.EqualTo("this 'is' valid"));
                Assert.That(result[1], Is.EqualTo("so \"is\" this"));
            });
        }
    }
}
