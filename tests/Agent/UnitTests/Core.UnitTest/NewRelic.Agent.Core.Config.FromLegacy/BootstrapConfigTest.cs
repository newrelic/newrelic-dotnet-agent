// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NUnit.Framework;

namespace NewRelic.Agent.Core.Config
{
    [TestFixture]
    public class BootstrapConfigTest
    {
        [Test]
        public void TestInvalidServiceAttribute()
        {
            var bogusConfigXml = "<configuration xmlns=\"urn:newrelic-config\">" +
                                 "<service bogus=\"true\" licenseKey=\"dude\"/>" +
                                 "<application><name>My App</name></application>" +
                                 "</configuration>";

            Func<string> configSchemaSource = () => Properties.Resources.Configuration;

            using (var logging = new TestUtilities.Logging())
            {
                // this should load with an error in the event log
                ConfigurationLoader.InitializeFromXml(bogusConfigXml, configSchemaSource);

                var errorMessage = Type.GetType("Mono.Runtime") == null ?
                        "An error occurred parsing newrelic.config - The 'bogus' attribute is not declared." :
                        "An error occurred parsing newrelic.config - XmlSchema error: Attribute declaration was not found for bogus";
                Assert.IsTrue(logging.HasMessageThatContains(errorMessage));
            }
        }
    }
}
