// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using Newtonsoft.Json;
using NUnit.Framework;

namespace NewRelic.Agent.Core.DataTransport
{
    [TestFixture]
    public class CollectorResponseEnvelopeTests
    {
        [Test]
        public void deserializes_from_successful_response()
        {
            const string json = @"{""return_value"": ""Hello!""}";

            var result = JsonConvert.DeserializeObject<CollectorResponseEnvelope<string>>(json);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.ReturnValue, Is.EqualTo("Hello!"));
        }
    }
}
