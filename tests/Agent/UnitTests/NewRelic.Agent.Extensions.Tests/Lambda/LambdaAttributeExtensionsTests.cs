// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Lambda;
using NUnit.Framework;
using Telerik.JustMock;

namespace Agent.Extensions.Tests.Lambda
{
    public class LambdaAttributeExtensionsTests
    {
        [Test]
        public void AddEventSourceAttribute_AddsToDictionary()
        {
            var dict = new Dictionary<string, string>();
            dict.AddEventSourceAttribute("suffix", "value");

            Assert.That(dict.Count, Is.EqualTo(1));
            Assert.That(dict["aws.lambda.eventSource.suffix"], Is.EqualTo("value"));
        }
        [Test]
        public void AddLambdaAttributes_AddsToTransaction()
        {
            var transaction = Mock.Create<ITransaction>();
            Dictionary<string, string> actualCustomAttributes = new();;
            Mock.Arrange(() => transaction.AddLambdaAttribute(Arg.IsAny<string>(), Arg.IsAny<string>()))
                .DoInstead((string key, string value) => actualCustomAttributes.Add(key, value));

            var attributes = new Dictionary<string, string>
            {
                { "key1", "value1" },
                { "key2", "value2" }
            };

            transaction.AddLambdaAttributes(attributes);

            Assert.That(actualCustomAttributes.Count, Is.EqualTo(2));
            Assert.That(actualCustomAttributes["key1"], Is.EqualTo("value1"));
            Assert.That(actualCustomAttributes["key2"], Is.EqualTo("value2"));
        }
    }
}
