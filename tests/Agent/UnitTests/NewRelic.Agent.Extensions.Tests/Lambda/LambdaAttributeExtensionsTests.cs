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
        public void AddEventSourceAttribute_AddsToTransaction()
        {
            var transaction = Mock.Create<ITransaction>();
            Dictionary<string, string> actualCustomAttributes = new();
            Mock.Arrange(() => transaction.AddLambdaAttribute(Arg.IsAny<string>(), Arg.IsAny<string>()))
                .DoInstead((string key, string value) => actualCustomAttributes.Add(key, value));

            transaction.AddEventSourceAttribute("key1", "value1");
            transaction.AddEventSourceAttribute("key2", "value2");

            Assert.That(actualCustomAttributes.Count, Is.EqualTo(2));
            Assert.That(actualCustomAttributes["aws.lambda.eventSource.key1"], Is.EqualTo("value1"));
            Assert.That(actualCustomAttributes["aws.lambda.eventSource.key2"], Is.EqualTo("value2"));
        }
    }
}
