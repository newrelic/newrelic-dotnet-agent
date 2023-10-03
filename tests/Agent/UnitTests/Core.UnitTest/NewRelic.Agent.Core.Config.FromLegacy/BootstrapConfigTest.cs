// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.IO;
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

            var xsdFile = Path.Combine(TestContext.CurrentContext.TestDirectory, "Configuration.xsd");
            Func<string> configSchemaSource = () => File.ReadAllText(xsdFile);

            using (var logging = new TestUtilities.Logging())
            {
                // this should load with an error in the event log
                ConfigurationLoader.InitializeFromXml(bogusConfigXml, configSchemaSource);

                var errorMessage = Type.GetType("Mono.Runtime") == null ?
                        "The 'bogus' attribute is not declared" :
                        "XmlSchema error: Attribute declaration was not found for bogus";
                Assert.IsTrue(logging.HasErrorMessageThatContains(errorMessage));
            }
        }

        [Test]
        public void TestMissingOrEmptyConfigXsd()
        {
            // The config XML is irrelvant to this test, so just copied this from the other test
            var bogusConfigXml = "<configuration xmlns=\"urn:newrelic-config\">" +
                                 "<service bogus=\"true\" licenseKey=\"dude\"/>" +
                                 "<application><name>My App</name></application>" +
                                 "</configuration>";

            // Have the config schema source yield an empty string, as would happen if the
            // actual newrelic.xsd on disk was missing or empty
            Func<string> configSchemaSource = () => string.Empty;

            using (var logging = new TestUtilities.Logging())
            {
                // this should load with an error in the event log
                ConfigurationLoader.InitializeFromXml(bogusConfigXml, configSchemaSource);

                // While this error message is somewhat cryptic, in an actual agent run it would be
                // preceeded by a warning message regarding failure to read the schema file contents from disk
                var errorMessage = "Root element is missing";
                Assert.IsTrue(logging.HasErrorMessageThatContains(errorMessage));
            }
        }
    }
}
