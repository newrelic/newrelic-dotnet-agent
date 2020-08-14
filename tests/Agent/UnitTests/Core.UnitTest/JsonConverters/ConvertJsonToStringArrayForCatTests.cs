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

            Assert.AreEqual(7, result.Length);
            Assert.AreEqual("1", result[0]);
            Assert.AreEqual("single quoted string", result[1]);
            Assert.AreEqual("True", result[2]);
            Assert.AreEqual("double quoted string", result[3]);
            Assert.AreEqual(null, result[4]);
            Assert.AreEqual("4.32", result[5]);
            Assert.AreEqual("5", result[6]);
        }

        [Test]
        public void Deserialize_ArraySizeMatchesUpperBound()
        {
            var result = CrossApplicationTracingJsonHelper.ConvertJsonToStringArrayForCat(_wellformedJson, 0, 7);
            Assert.AreEqual(7, result.Length);

            result = CrossApplicationTracingJsonHelper.ConvertJsonToStringArrayForCat(_wellformedJson, 0, 10);
            Assert.AreEqual(10, result.Length);
        }

        public void Deserialize_EmptyJsonArray_ResultsInNotNull()
        {
            var result = CrossApplicationTracingJsonHelper.ConvertJsonToStringArrayForCat(_wellformedJson_EmptyArray, 0, 7);

            Assert.IsNotNull(result);
            Assert.AreEqual(7, result.Length);
            Assert.IsTrue(result.All(x => x == null));
        }

        [Test]
        public void Deserialize_OversizedJsonArray_ResultsInNull()
        {
            for (var i = 0; i < 7; i++)
            {
                var resultExpectedNull = CrossApplicationTracingJsonHelper.ConvertJsonToStringArrayForCat(_wellformedJson, 0, i);
                Assert.IsNull(resultExpectedNull);
            }

            var resultExpectedNotNull = CrossApplicationTracingJsonHelper.ConvertJsonToStringArrayForCat(_wellformedJson, 0, 8);
            Assert.IsNotNull(resultExpectedNotNull);
        }

        [Test]
        public void Deserialize_UndersizedJsonArray_ResultsInNull()
        {
            var resultExpectedNull = CrossApplicationTracingJsonHelper.ConvertJsonToStringArrayForCat(_wellformedJson, 8, 100);
            Assert.IsNull(resultExpectedNull);

            for (var i = 0; i <= 7; i++)
            {
                var resultExpectedNotNull = CrossApplicationTracingJsonHelper.ConvertJsonToStringArrayForCat(_wellformedJson, i, 100);
                Assert.IsNotNull(resultExpectedNotNull);
            }
        }

        [Test]
        public void Deserialize_WellFormedJson_MalformedJsonArray_ResultsInNull()
        {
            var resultExpectedNull = CrossApplicationTracingJsonHelper.ConvertJsonToStringArrayForCat(_malformedJson_ArrayNotClosed, 0, 100);
            Assert.IsNull(resultExpectedNull);

            resultExpectedNull = CrossApplicationTracingJsonHelper.ConvertJsonToStringArrayForCat(_wellformedJson_NotAnArray1, 0, 100);
            Assert.IsNull(resultExpectedNull);

            resultExpectedNull = CrossApplicationTracingJsonHelper.ConvertJsonToStringArrayForCat(_wellformedJson_NotAnArray2, 0, 100);
            Assert.IsNull(resultExpectedNull);
        }

        [Test]
        public void Deserialize_WellFormedJson_BooleansMatchDotNetStringRepresentation()
        {
            var result = CrossApplicationTracingJsonHelper.ConvertJsonToStringArrayForCat(_wellformedJson_Booleans, 0, 3);
            Assert.AreEqual(true.ToString(), result[0]);
            Assert.AreEqual(false.ToString(), result[1]);
            Assert.AreEqual(true.ToString(), result[2]);
            Assert.AreNotEqual("true", result[0]);
            Assert.AreNotEqual("false", result[1]);
        }

        [Test]
        public void Deserialize_EmptyString_ResultsInNull()
        {
            var resultExpectedNull = CrossApplicationTracingJsonHelper.ConvertJsonToStringArrayForCat(string.Empty, 0, 100);
            Assert.IsNull(resultExpectedNull);

            resultExpectedNull = CrossApplicationTracingJsonHelper.ConvertJsonToStringArrayForCat(" ", 0, 100);
            Assert.IsNull(resultExpectedNull);
        }

        [Test]
        public void Deserialize_EmptyElement_ResultsInNull()
        {
            var resultExpectedNull = CrossApplicationTracingJsonHelper.ConvertJsonToStringArrayForCat(_malformedJson_EmptyElement, 0, 100);
            Assert.IsNull(resultExpectedNull);
        }

        [Test]
        public void Deserialize_EmbeddedArray_ResultsInNull()
        {
            var resultExpectedNull = CrossApplicationTracingJsonHelper.ConvertJsonToStringArrayForCat(_wellformedJson_EmbeddedArray, 0, 100);
            Assert.IsNull(resultExpectedNull);
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

            Assert.AreEqual("this 'is' valid", result[0]);
            Assert.AreEqual("so \"is\" this", result[1]);
        }
    }
}
