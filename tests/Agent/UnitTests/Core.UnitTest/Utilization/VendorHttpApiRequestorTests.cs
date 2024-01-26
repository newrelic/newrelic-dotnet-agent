// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NUnit.Framework;
using System;

namespace NewRelic.Agent.Core.Utilization
{
    [TestFixture]
    public class VendorHttpApiRequestorTests
    {
        private Uri BogusUri = new Uri("http://bogus.api.not");

        [Test]
        public void CallVendorApi_LogsException()
        {
            using (var logging = new TestUtilities.Logging())
            {
                var requestor = new VendorHttpApiRequestor();
                var response = requestor.CallVendorApi(BogusUri, "GET", "bogus");

                Assert.Multiple(() =>
                {
                    Assert.That(response, Is.Null);
                    Assert.That(logging.HasMessageThatContains("CallVendorApi"), Is.True);
                });
            }
        }
    }
}
