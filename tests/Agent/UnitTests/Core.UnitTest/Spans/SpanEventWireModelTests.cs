// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Core.Attributes;
using NewRelic.Agent.TestUtilities;
using NewRelic.Agent.Core.Segments;
using Newtonsoft.Json;
using NUnit.Framework;
using System.Collections.Generic;
using System;
using Telerik.JustMock;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Fixtures;

namespace NewRelic.Agent.Core.Spans.Tests
{
    [TestFixture]
    class SpanEventWireModelTests
    {
        private ConfigurationAutoResponder _configAutoResponder;

        private IAttributeDefinitionService _attribDefSvc;

        private IAttributeDefinitions _attribDefs => _attribDefSvc?.AttributeDefs;

        [SetUp]
        public void SetUp()
        {
            _configAutoResponder = new ConfigurationAutoResponder(GetConfiguration());
            _attribDefSvc = new AttributeDefinitionService((f) => new AttributeDefinitions(f));
        }

        [TearDown]
        public void TearDown()
        {
            _configAutoResponder.Dispose();
        }

        private IConfiguration GetConfiguration()
        {
            
            var config = Mock.Create<IConfiguration>();

            Mock.Arrange(() => config.ConfigurationVersion).Returns(int.MaxValue);
            Mock.Arrange(() => config.SpanEventsEnabled).Returns(true);
            Mock.Arrange(() => config.CaptureAttributes).Returns(true);
            Mock.Arrange(() => config.DistributedTracingEnabled).Returns(true);

            return config;
        }

        [Test]
        public void SpanEventWireModelTests_Serialization()
        {
            const float priority = 1.975676f;
            var duration = TimeSpan.FromSeconds(4.81);

            var expectedSerialization = new Dictionary<string, object>[3]
            {
                new Dictionary<string,object>
                {
                    { "type", "Span"},
                    { "priority", priority},
                    { "traceId", "ed5bbf27f28ebef3" },
                    { "duration", duration.TotalSeconds }
                },
                new Dictionary<string, object>
                {

                },
                new Dictionary<string, object>
                {
                    { "http.request.method","GET" }
                }
            };

            var spanEventWireModel = new SpanAttributeValueCollection();
            spanEventWireModel.Priority = priority;
            _attribDefs.GetTypeAttribute(TypeAttributeValue.Span).TrySetDefault(spanEventWireModel);
            _attribDefs.Priority.TrySetValue(spanEventWireModel, priority);
            _attribDefs.Duration.TrySetValue(spanEventWireModel, duration);
            _attribDefs.DistributedTraceId.TrySetValue(spanEventWireModel, "ed5bbf27f28ebef3");
            _attribDefs.HttpMethod.TrySetValue(spanEventWireModel, "GET");

            var serialized = JsonConvert.SerializeObject(spanEventWireModel);
            Assert.That(serialized, Is.Not.Null);

            var deserialized = JsonConvert.DeserializeObject<Dictionary<string, object>[]>(serialized);
            Assert.That(deserialized, Is.Not.Null);

            AttributeComparer.CompareDictionaries(expectedSerialization, deserialized);
        }

        [Test]
        public void SpanEventWireModelTests_MultipleEvents_Serialization()
        {
            const float priority = 1.975676f;
            var duration = TimeSpan.FromSeconds(4.81);

            var expectedSerialization = new List<Dictionary<string, object>[]>
            {
                new[] {
                    new Dictionary<string,object>
                    {
                        { "type", "Span"},
                        { "priority", priority},
                        { "traceId", "ed5bbf27f28ebef3" },
                        { "duration", duration.TotalSeconds }
                    },
                    new Dictionary<string, object>
                    {

                    },
                    new Dictionary<string, object>
                    {
                        { "http.request.method","GET" }
                    }
                },
                new[] {
                    new Dictionary<string,object>
                    {
                        { "type", "Span"},
                        { "priority", priority - 1},
                        { "traceId", "fa5bbf27f28ebef3" },
                        { "duration", duration.TotalSeconds - 1}
                    },
                    new Dictionary<string, object>
                    {

                    },
                    new Dictionary<string, object>
                    {
                        { "http.request.method","POST" }
                    }
                }
            };

            var spanEvents = new[]
            {
                CreateSpanEvent(priority, duration, "ed5bbf27f28ebef3", "GET"),
                CreateSpanEvent(priority -1, duration.Subtract(TimeSpan.FromSeconds(1)), "fa5bbf27f28ebef3", "POST")
            };

            var serialized = JsonConvert.SerializeObject(spanEvents);
            Assert.That(serialized, Is.Not.Null);

            var deserialized = JsonConvert.DeserializeObject<List<Dictionary<string, object>[]>>(serialized);
            Assert.That(deserialized, Is.Not.Null);

            Assert.AreEqual(expectedSerialization.Count, deserialized.Count);
            AttributeComparer.CompareDictionaries(expectedSerialization[0], deserialized[0]);
            AttributeComparer.CompareDictionaries(expectedSerialization[1], deserialized[1]);
        }

        private ISpanEventWireModel CreateSpanEvent(float priority, TimeSpan duration, string traceId, string httpMethod)
        {
            var wireModel = new SpanAttributeValueCollection();
            wireModel.Priority = priority;
            _attribDefs.GetTypeAttribute(TypeAttributeValue.Span).TrySetDefault(wireModel);
            _attribDefs.Priority.TrySetValue(wireModel, priority);
            _attribDefs.Duration.TrySetValue(wireModel, duration);
            _attribDefs.DistributedTraceId.TrySetValue(wireModel, traceId);
            _attribDefs.HttpMethod.TrySetValue(wireModel, httpMethod);

            return wireModel;
        }
    }
}

