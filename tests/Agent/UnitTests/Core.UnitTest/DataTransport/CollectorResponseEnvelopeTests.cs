// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using Newtonsoft.Json;

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

            ClassicAssert.NotNull(result);
            ClassicAssert.AreEqual("Hello!", result.ReturnValue);
        }
    }
}
