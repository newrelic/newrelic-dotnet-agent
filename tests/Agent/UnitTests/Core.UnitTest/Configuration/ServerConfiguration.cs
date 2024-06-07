// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using NUnit.Framework;

namespace NewRelic.Agent.Core.Configuration.UnitTest
{
    [TestFixture, Category("Configuration")]
    public class Method_FromDeserializedReturnValue
    {
        [Test]
        public void when_collection_is_empty_then_does_throw_exception()
        {
            Assert.Throws(Is.InstanceOf<Exception>(), () => ServerConfiguration.FromDeserializedReturnValue(new Dictionary<string, object>()));
        }

        [Test]
        public void unable_to_cast_throws_exception()
        {
            Assert.Throws(Is.InstanceOf<Exception>(), () => ServerConfiguration.FromDeserializedReturnValue(new Dictionary<string, object> { { "apdex_t", "hello" } }));
        }

        [Test]
        public void bool_converts_correctly()
        {
            var serverConfiguration = ServerConfiguration.FromDeserializedReturnValue(new Dictionary<string, object> { { "agent_run_id", 0 }, { "collect_analytics_events", false } });
            Assert.That(serverConfiguration.AnalyticsEventCollectionEnabled, Is.EqualTo(false));
        }

        [Test]
        public void decimal_converts_correctly()
        {
            var serverConfiguration = ServerConfiguration.FromDeserializedReturnValue(new Dictionary<string, object> { { "agent_run_id", 0 }, { "apdex_t", 1.2m } });
            Assert.That(serverConfiguration.ApdexT, Is.EqualTo(1.2));
        }

        [Test]
        public void double_converts_correctly()
        {
            var serverConfiguration = ServerConfiguration.FromDeserializedReturnValue(new Dictionary<string, object> { { "agent_run_id", 0 }, { "apdex_t", 1.2d } });
            Assert.That(serverConfiguration.ApdexT, Is.EqualTo(1.2));
        }

        [Test]
        public void string_converts_correctly()
        {
            var serverConfiguration = ServerConfiguration.FromDeserializedReturnValue(new Dictionary<string, object> { { "agent_run_id", 0 }, { "application_id", "Bacon!" } });
            Assert.That(serverConfiguration.RumSettingsApplicationId, Is.EqualTo("Bacon!"));
        }

        [Test]
        public void Int32_converts_correctly()
        {
            var serverConfiguration = ServerConfiguration.FromDeserializedReturnValue(new Dictionary<string, object> { { "agent_run_id", (int)1234 } });
            Assert.That(serverConfiguration.AgentRunId, Is.EqualTo(1234));
        }


        [Test]
        public void NullableInt32_converts_correctly()
        {
            var serverConfiguration = ServerConfiguration.FromDeserializedReturnValue(new Dictionary<string, object> { { "agent_run_id", (long)1234 }, { "sampling_rate", (int?)1357 } });
            Assert.That(serverConfiguration.SamplingRate, Is.EqualTo(1357));
        }

        [Test]
        public void NullableInt32_converts_correctly_when_no_optional_value_is_provided()
        {
            var serverConfiguration = ServerConfiguration.FromDeserializedReturnValue(new Dictionary<string, object> { { "agent_run_id", (long)1234 } });
            Assert.That(serverConfiguration.SamplingRate, Is.Null);
        }

        [Test]
        public void Int64_converts_correctly()
        {
            var serverConfiguration = ServerConfiguration.FromDeserializedReturnValue(new Dictionary<string, object> { { "agent_run_id", (long)1234 } });
            Assert.That(serverConfiguration.AgentRunId, Is.EqualTo(1234));
        }

        [Test]
        public void string_when_double_expected_then_throws_exception()
        {
            Assert.Throws(Is.InstanceOf<Exception>(), () => ServerConfiguration.FromDeserializedReturnValue(new Dictionary<string, object> { { "agent_run_id", 0 }, { "apdex_t", "not a double" } }));
        }

    }

    [TestFixture, Category("Configuration")]
    public class Method_FromJson
    {
        [Test]
        public void when_agent_config_is_missing_from_json_then_RpmConfig_is_not_null()
        {
            var serverConfiguration = ServerConfiguration.FromJson("{\"agent_run_id\":42}");
            Assert.That(serverConfiguration.RpmConfig, Is.Not.Null);
        }

        [Test]
        public void when_json_string_does_not_contain_required_fields_then_exception_is_thrown()
        {
            Assert.Throws<JsonSerializationException>(() => ServerConfiguration.FromJson("{}"));
        }

        [TestCase("{\"agent_run_id\":42,\"agent_config\":{\"error_collector.expected_status_codes\":[401,402]}}", ExpectedResult = new[] { "401", "402" })]
        [TestCase("{\"agent_run_id\":42,\"agent_config\":{\"error_collector.expected_status_codes\":[\"401\",\"402\"]}}", ExpectedResult = new[] { "401", "402" })]
        public IEnumerable<string> can_deserialize_integer_array_or_string_array_for_expected_status_codes(string json)
        {
            var serverConfiguration = ServerConfiguration.FromJson(json);
            return serverConfiguration.RpmConfig.ErrorCollectorExpectedStatusCodes;
        }

        [TestCase(true, ExpectedResult = null)]
        [TestCase(false, ExpectedResult = true)]
        public bool? when_ignore_server_config_set_agent_config_is_ignored(bool ignoreServerConfig)
        {
            var serverConfiguration = ServerConfiguration.FromJson(@"{""agent_run_id"":42,""agent_config"": {""slow_sql.enabled"":true}}", ignoreServerConfig);
            return serverConfiguration.RpmConfig.SlowSqlEnabled;
        }
    }

    [TestFixture]
    public class Method_JsonContainsNonNullProperty
    {
        [Test]
        [TestCase(@"{""foo"": 1, ""bar"": 2}", ExpectedResult = true)]
        [TestCase(@"{""foo"": 1, ""bar"": {}}", ExpectedResult = true)]
        [TestCase(@"{""foo"": 1, ""bar"": []}", ExpectedResult = true)]
        [TestCase(@"{""foo"": 1, ""bar"": null}", ExpectedResult = false)]
        [TestCase(@"{""foo"": 1}", ExpectedResult = false)]
        public bool returns_true_if_json_contains_non_null_matching_property(string json)
        {
            return ServerConfiguration.JsonContainsNonNullProperty(json, "bar");
        }
    }

}
