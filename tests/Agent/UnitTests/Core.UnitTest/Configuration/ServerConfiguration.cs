using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using Newtonsoft.Json;
using NUnit.Framework;

// ReSharper disable InconsistentNaming
// ReSharper disable CheckNamespace
namespace NewRelic.Agent.Core.Configuration.UnitTest
{
    [TestFixture, Category("Configuration")]
    public class Method_FromDeserializedReturnValue
    {
        [Test]
        public void when_collection_is_empty_then_does_throw_exception()
        {
            Assert.Throws(Is.InstanceOf<Exception>(), () => ServerConfiguration.FromDeserializedReturnValue(new Dictionary<String, Object>()));
        }

        [Test]
        public void unable_to_cast_throws_exception()
        {
            Assert.Throws(Is.InstanceOf<Exception>(), () => ServerConfiguration.FromDeserializedReturnValue(new Dictionary<String, Object> { { "apdex_t", "hello" } }));
        }

        [Test]
        public void bool_converts_correctly()
        {
            var serverConfiguration = ServerConfiguration.FromDeserializedReturnValue(new Dictionary<String, Object> { { "agent_run_id", 0 }, { "collect_analytics_events", false } });
            Assert.AreEqual(false, serverConfiguration.AnalyticsEventCollectionEnabled);
        }

        [Test]
        public void decimal_converts_correctly()
        {
            var serverConfiguration = ServerConfiguration.FromDeserializedReturnValue(new Dictionary<String, Object> { { "agent_run_id", 0 }, { "apdex_t", 1.2m } });
            Assert.AreEqual(1.2, serverConfiguration.ApdexT);
        }

        [Test]
        public void double_converts_correctly()
        {
            var serverConfiguration = ServerConfiguration.FromDeserializedReturnValue(new Dictionary<String, Object> { { "agent_run_id", 0 }, { "apdex_t", 1.2d } });
            Assert.AreEqual(1.2, serverConfiguration.ApdexT);
        }

        [Test]
        public void string_converts_correctly()
        {
            var serverConfiguration = ServerConfiguration.FromDeserializedReturnValue(new Dictionary<String, Object> { { "agent_run_id", 0 }, { "application_id", "Bacon!" } });
            Assert.AreEqual("Bacon!", serverConfiguration.RumSettingsApplicationId);
        }

        [Test]
        public void Int32_converts_correctly()
        {
            var serverConfiguration = ServerConfiguration.FromDeserializedReturnValue(new Dictionary<String, Object> { { "agent_run_id", (Int32)1234 } });
            Assert.AreEqual(1234, serverConfiguration.AgentRunId);
        }


        [Test]
        public void NullableInt32_converts_correctly()
        {
            var serverConfiguration = ServerConfiguration.FromDeserializedReturnValue(new Dictionary<String, Object> { { "agent_run_id", (Int64)1234 }, { "sampling_rate", (Int32?)1357 } });
            Assert.AreEqual(1357, serverConfiguration.SamplingRate);
        }

        [Test]
        public void NullableInt32_converts_correctly_when_no_optional_value_is_provided()
        {
            var serverConfiguration = ServerConfiguration.FromDeserializedReturnValue(new Dictionary<String, Object> { { "agent_run_id", (Int64)1234 } });
            Assert.IsNull(serverConfiguration.SamplingRate);
        }

        [Test]
        public void Int64_converts_correctly()
        {
            var serverConfiguration = ServerConfiguration.FromDeserializedReturnValue(new Dictionary<String, Object> { { "agent_run_id", (Int64)1234 } });
            Assert.AreEqual(1234, serverConfiguration.AgentRunId);
        }

        [Test]
        public void string_when_double_expected_then_throws_exception()
        {
            Assert.Throws(Is.InstanceOf<Exception>(), () => ServerConfiguration.FromDeserializedReturnValue(new Dictionary<String, Object> { { "agent_run_id", 0 }, { "apdex_t", "not a double" } }));
        }

    }

    [TestFixture, Category("Configuration")]
    public class Method_FromJson
    {
        [Test]
        public void when_agent_config_is_missing_from_json_then_RpmConfig_is_not_null()
        {
            var serverConfiguration = ServerConfiguration.FromJson("{\"agent_run_id\":42}");
            Assert.IsNotNull(serverConfiguration.RpmConfig);
        }

        [Test]
        public void when_json_string_does_not_contain_required_fields_then_exception_is_thrown()
        {
            Assert.Throws<JsonSerializationException>(() => ServerConfiguration.FromJson("{}"));
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
        public Boolean returns_true_if_json_contains_non_null_matching_property([NotNull] String json)
        {
            return ServerConfiguration.JsonContainsNonNullProperty(json, "bar");
        }
    }

}
