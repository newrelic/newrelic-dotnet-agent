// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace NewRelic.Agent.Core.Config
{

    [TestFixture]
    public class ConfigurationParserTest
    {
        private TestConfig testConfig;
        class TestConfig
        {
            [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode"),
                ConfigurationAttribute("test")]
            public float Test { get; set; }

            [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode"),
                System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode"),
                ConfigurationAttribute("test2")]
            public bool Test2 { get; set; }

            [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode"),
                System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode"),
                ConfigurationAttribute("testInt32")]
            public int TestInt32 { get; set; }


            [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic"),
            System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode"),
                System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA1801:ReviewUnusedParameters", MessageId = "value"),
                ConfigurationAttribute("test_error")]
            public int TestError
            {
                set
                {
                    object test = null;
                    Console.Write(test.ToString());
                }
            }

            [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic"),
            System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode"),
                ConfigurationAttribute("test_null")]
            public string TestNull
            {
                set
                {
                    Console.Write(value.ToString());
                }
            }
        }

        [SetUp]
        public void SetUp()
        {
            this.testConfig = new TestConfig();
        }

        [Test]
        public void TestSuccess()
        {
            Dictionary<string, object> config = new Dictionary<string, object>() { { "test", 6f }, { "test2", true } };

            new ConfigurationParser(testConfig).ParseConfiguration(config);

            Assert.AreEqual(6f, testConfig.Test);
        }

        [Test]
        public void TestInvalidCast()
        {
            Dictionary<string, object> config = new Dictionary<string, object>() { { "test2", 6f } };
            try
            {
                new ConfigurationParser(testConfig).ParseConfiguration(config);
                Assert.Fail();
            }
            catch (InvalidCastException ex)
            {
                Assert.AreEqual("Unable to cast configuration value \"test2\".  The value was 6 (System.Single)", ex.Message);
            }
        }

        [Test]
        public void Test32BitInteger_expect_success()
        {
            int expected = 99999552;
            Dictionary<string, object> config = new Dictionary<string, object>() { { "testInt32", expected } };
            new ConfigurationParser(testConfig).ParseConfiguration(config);
            Assert.AreEqual(expected, testConfig.TestInt32);
        }

        [Test]
        public void Test64BitInteger_expect_exception()
        {
            Dictionary<string, object> config = new Dictionary<string, object>() { { "testInt32", 9220002036854775807 } };
            try
            {
                new ConfigurationParser(testConfig).ParseConfiguration(config);
                Assert.Fail();
            }
            catch (InvalidCastException ex)
            {
                Assert.AreEqual("Unable to cast configuration value \"testInt32\".  The value was 9220002036854775807 (System.Int64)", ex.Message);
            }

        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes"), Test]
        public void TestParseNull()
        {
            Dictionary<string, object> config = new Dictionary<string, object>() { { "test_null", null } };
            try
            {
                new ConfigurationParser(testConfig).ParseConfiguration(config);
                Assert.Fail();
            }
            catch (Exception ex)
            {
                Assert.AreEqual("An error occurred parsing the configuration value \"test_null\".  The value was null", ex.Message);
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes"), Test]
        public void TestParseError()
        {
            Dictionary<string, object> config = new Dictionary<string, object>() { { "test_error", 33 } };
            try
            {
                new ConfigurationParser(testConfig).ParseConfiguration(config);
                Assert.Fail();
            }
            catch (Exception ex)
            {
                Assert.AreEqual("An error occurred parsing the configuration value \"test_error\".  "
                + "The value was 33 (System.Int32).  Error : Object reference not set to an instance of an object.",
                ex.Message);
            }
        }
    }
}
