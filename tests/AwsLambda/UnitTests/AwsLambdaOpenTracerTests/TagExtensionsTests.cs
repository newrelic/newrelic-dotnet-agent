// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.OpenTracing.AmazonLambda.Util;

namespace NewRelic.Tests.AwsLambda.AwsLambdaOpenTracerTests
{
    [TestFixture]
    public class TagExtensionsTests
    {
        [Test]
        public void ResponseStatusTagShouldCreateAppropriateAttributes()
        {
            var tag = new KeyValuePair<string, object>("response.status", "200");

            var attributes = tag.GetAttributes();

            var expectedAttributes = new[] { new KeyValuePair<string, object>("response.status", "200"), new KeyValuePair<string, object>("http.statusCode", 200) };
            CollectionAssert.AreEquivalent(expectedAttributes, attributes);
        }

        [Test]
        public void NonNumericResponseStatusTagShouldNotCreateHttpStatusCodeAttribute()
        {
            var tag = new KeyValuePair<string, object>("response.status", "foo");

            var attributes = tag.GetAttributes();

            var expectedAttributes = new[] { new KeyValuePair<string, object>("response.status", "foo") };
            CollectionAssert.AreEquivalent(expectedAttributes, attributes);
        }

        [Test]
        public void EmptyResponseStatusShouldNotCreateAttributes()
        {
            var tag = new KeyValuePair<string, object>("response.status", "");

            var attributes = tag.GetAttributes();

            CollectionAssert.IsEmpty(attributes);
        }

        [Test]
        public void HttpStatusCodeTagShouldCreateAppropriateAttributes()
        {
            var tag = new KeyValuePair<string, object>("http.status_code", 200);

            var attributes = tag.GetAttributes();

            var expectedAttributes = new[] { new KeyValuePair<string, object>("response.status", "200"), new KeyValuePair<string, object>("http.statusCode", 200) };
            CollectionAssert.AreEquivalent(expectedAttributes, attributes);
        }

        [Test]
        public void NullTagValueShouldNotCreateAttributes()
        {
            var tag = new KeyValuePair<string, object>("foo", null);

            var attributes = tag.GetAttributes();

            CollectionAssert.IsEmpty(attributes);
        }

        [Test]
        public void TagShouldCreateSingleAttribute()
        {
            var tag = new KeyValuePair<string, object>("foo", "bar");

            var attributes = tag.GetAttributes();

            var expectedAttributes = new[] { new KeyValuePair<string, object>("foo", "bar") };
            CollectionAssert.AreEquivalent(expectedAttributes, attributes);
        }

        [TestCase("aws.something", ExpectedResult = true)]
        [TestCase("span.something", ExpectedResult = true)]
        [TestCase("peer.something", ExpectedResult = true)]
        [TestCase("db.something", ExpectedResult = true)]
        [TestCase("component", ExpectedResult = true)]
        [TestCase("error", ExpectedResult = true)]
        [TestCase("http.something", ExpectedResult = true)]
        [TestCase("request.something", ExpectedResult = true)]
        [TestCase("response.something", ExpectedResult = true)]
        [TestCase("foo", ExpectedResult = false)]
        [TestCase("foo.bar", ExpectedResult = false)]
        [TestCase("aws", ExpectedResult = false)]
        [TestCase("span", ExpectedResult = false)]
        [TestCase("peer", ExpectedResult = false)]
        [TestCase("db", ExpectedResult = false)]
        [TestCase("component.something", ExpectedResult = false)]
        [TestCase("error.something", ExpectedResult = false)]
        [TestCase("http", ExpectedResult = false)]
        [TestCase("request", ExpectedResult = false)]
        [TestCase("response", ExpectedResult = false)]
        public bool IsAgentAttributeShouldReturnCorrectValue(string tagName)
        {
            var tag = new KeyValuePair<string, object>(tagName, null);

            return tag.IsAgentAttribute();
        }

        [Test]
        public void BuildUserAttributesResultsOnlyUserAttributes()
        {
            var tags = new Dictionary<string, object> { { "foo", "value" }, { "aws.something", "value" } };

            var attributes = tags.BuildUserAttributes();

            var expectedAttributes = new Dictionary<string, object> { { "foo", "value" } };
            CollectionAssert.AreEquivalent(expectedAttributes, attributes);
        }

        [Test]
        public void BuildAgentAttributesResultsOnlyUserAttributes()
        {
            var tags = new Dictionary<string, object> { { "foo", "value" }, { "aws.something", "value" } };

            var attributes = tags.BuildAgentAttributes();

            var expectedAttributes = new Dictionary<string, object> { { "aws.something", "value" } };
            CollectionAssert.AreEquivalent(expectedAttributes, attributes);
        }
    }
}
