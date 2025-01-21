// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Configuration;
using NewRelic.Agent.Core.DataTransport;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.DataTransport.Tests
{
    [TestFixture]
    public class SecurityPoliciesSettingsModelTests
    {
        [Test]
        public void TestJsonSerialization()
        {
            var configuration = Mock.Create<IConfiguration>();

            Mock.Arrange(() => configuration.TransactionTracerRecordSql).Returns(DefaultConfiguration.ObfuscatedStringValue);
            Mock.Arrange(() => configuration.CanUseAttributesIncludes).Returns(true);
            Mock.Arrange(() => configuration.StripExceptionMessages).Returns(false);
            Mock.Arrange(() => configuration.CustomEventsEnabled).Returns(true);
            Mock.Arrange(() => configuration.CaptureCustomParameters).Returns(true);
            Mock.Arrange(() => configuration.CustomInstrumentationEditorEnabled).Returns(true);

            var model = new SecurityPoliciesSettingsModel(configuration);
            var json = JsonConvert.SerializeObject(model);

            Assert.Multiple(() =>
            {
                Assert.That(json, Is.Not.Null);
                Assert.That(json, Does.Contain("\"record_sql\":{\"enabled\":true}"));
                Assert.That(json, Does.Contain("\"attributes_include\":{\"enabled\":true}"));
                Assert.That(json, Does.Contain("\"allow_raw_exception_messages\":{\"enabled\":true}"));
                Assert.That(json, Does.Contain("\"custom_events\":{\"enabled\":true}"));
                Assert.That(json, Does.Contain("\"custom_parameters\":{\"enabled\":true}"));
                Assert.That(json, Does.Contain("\"custom_instrumentation_editor\":{\"enabled\":true}"));
            });
        }

        [Test]
        public void TestJsonSerializationWithFalseValues()
        {
            var configuration = Mock.Create<IConfiguration>();

            Mock.Arrange(() => configuration.TransactionTracerRecordSql).Returns(DefaultConfiguration.ObfuscatedStringValue);
            Mock.Arrange(() => configuration.CanUseAttributesIncludes).Returns(false);
            Mock.Arrange(() => configuration.StripExceptionMessages).Returns(true);
            Mock.Arrange(() => configuration.CustomEventsEnabled).Returns(false);
            Mock.Arrange(() => configuration.CaptureCustomParameters).Returns(false);
            Mock.Arrange(() => configuration.CustomInstrumentationEditorEnabled).Returns(false);

            var model = new SecurityPoliciesSettingsModel(configuration);
            var json = JsonConvert.SerializeObject(model);

            Assert.Multiple(() =>
            {
                Assert.That(json, Is.Not.Null);
                Assert.That(json, Does.Contain("\"record_sql\":{\"enabled\":true}"));
                Assert.That(json, Does.Contain("\"attributes_include\":{\"enabled\":false}"));
                Assert.That(json, Does.Contain("\"allow_raw_exception_messages\":{\"enabled\":false}"));
                Assert.That(json, Does.Contain("\"custom_events\":{\"enabled\":false}"));
                Assert.That(json, Does.Contain("\"custom_parameters\":{\"enabled\":false}"));
                Assert.That(json, Does.Contain("\"custom_instrumentation_editor\":{\"enabled\":false}"));
            });
        }

        [Test]
        public void TestInvalidRecordSqlSetting()
        {
            var configuration = Mock.Create<IConfiguration>();

            Mock.Arrange(() => configuration.TransactionTracerRecordSql).Returns(DefaultConfiguration.RawStringValue);

            Assert.Throws<ArgumentException>(() => new SecurityPoliciesSettingsModel(configuration));
        }

        [Test]
        public void TestDefaultValues()
        {
            var configuration = Mock.Create<IConfiguration>();

            Mock.Arrange(() => configuration.TransactionTracerRecordSql).Returns(DefaultConfiguration.ObfuscatedStringValue);
            Mock.Arrange(() => configuration.CanUseAttributesIncludes).Returns(false);
            Mock.Arrange(() => configuration.StripExceptionMessages).Returns(true);
            Mock.Arrange(() => configuration.CustomEventsEnabled).Returns(false);
            Mock.Arrange(() => configuration.CaptureCustomParameters).Returns(false);
            Mock.Arrange(() => configuration.CustomInstrumentationEditorEnabled).Returns(false);

            var model = new SecurityPoliciesSettingsModel(configuration);

            Assert.Multiple(() =>
            {
                Assert.That(model.RecordSql["enabled"], Is.True);
                Assert.That(model.AttributesInclude["enabled"], Is.False);
                Assert.That(model.AllowRawExceptionMessages["enabled"], Is.False);
                Assert.That(model.CustomEvents["enabled"], Is.False);
                Assert.That(model.CustomParameters["enabled"], Is.False);
                Assert.That(model.CustomInstrumentationEditor["enabled"], Is.False);
            });
        }

        [Test]
        public void TestNullConfiguration()
        {
            Assert.Throws<ArgumentNullException>(() => new SecurityPoliciesSettingsModel(null));
        }

        [Test]
        public void TestSerializationWithNullValues()
        {
            var configuration = Mock.Create<IConfiguration>();

            Mock.Arrange(() => configuration.TransactionTracerRecordSql).Returns((string)null);

            var model = new SecurityPoliciesSettingsModel(configuration);
            var json = JsonConvert.SerializeObject(model);

            Assert.Multiple(() =>
            {
                Assert.That(json, Is.Not.Null);
                Assert.That(json, Does.Contain("\"record_sql\":{\"enabled\":false}"));
                Assert.That(json, Does.Contain("\"attributes_include\":{\"enabled\":false}"));
                Assert.That(json, Does.Contain("\"allow_raw_exception_messages\":{\"enabled\":true}"));
                Assert.That(json, Does.Contain("\"custom_events\":{\"enabled\":false}"));
                Assert.That(json, Does.Contain("\"custom_parameters\":{\"enabled\":false}"));
                Assert.That(json, Does.Contain("\"custom_instrumentation_editor\":{\"enabled\":false}"));
            });
        }
    }
}
