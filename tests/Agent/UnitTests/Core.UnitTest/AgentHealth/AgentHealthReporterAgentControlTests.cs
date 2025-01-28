// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Text;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Fixtures;
using NewRelic.Agent.Core.Time;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Core.WireModels;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.AgentHealth
{
    public class AgentHealthReporterAgentControlTests
    {
        private AgentHealthReporter _agentHealthReporter;
        private ConfigurationAutoResponder _configurationAutoResponder;
        private List<MetricWireModel> _publishedMetrics;
        private IScheduler _scheduler;

        private void Setup(bool agentControlEnabled, string deliveryLocation, int frequency, IFileWrapper fileWrapper = null, IDirectoryWrapper directoryWrapper = null)
        {
            var configuration = Mock.Create<IConfiguration>();
            Mock.Arrange(() => configuration.AgentControlEnabled).Returns(agentControlEnabled);
            Mock.Arrange(() => configuration.HealthDeliveryLocation).Returns(deliveryLocation);
            Mock.Arrange(() => configuration.HealthFrequency).Returns(frequency);
            _configurationAutoResponder = new ConfigurationAutoResponder(configuration);

            var metricBuilder = WireModels.Utilities.GetSimpleMetricBuilder();
            _publishedMetrics = new List<MetricWireModel>();
            _scheduler = Mock.Create<IScheduler>();

            if (fileWrapper == null)
            {
                fileWrapper = Mock.Create<IFileWrapper>();
                Mock.Arrange(() => fileWrapper.Exists(Arg.IsAny<string>())).Returns(true);
                Mock.Arrange(() => fileWrapper.TryCreateFile(Arg.IsAny<string>(), Arg.IsAny<bool>())).Returns(true);
            }

            if (directoryWrapper == null)
            {
                directoryWrapper = Mock.Create<IDirectoryWrapper>();
                Mock.Arrange(() => directoryWrapper.Exists(Arg.IsAny<string>())).Returns(true);
            }

            _agentHealthReporter = new AgentHealthReporter(metricBuilder, _scheduler, fileWrapper, directoryWrapper);
            _agentHealthReporter.RegisterPublishMetricHandler(metric => _publishedMetrics.Add(metric));
        }

        [TearDown]
        public void TearDown()
        {
            _agentHealthReporter?.Dispose();
            _configurationAutoResponder?.Dispose();
        }

        [Test]
        public void AgentControlMetricPresent_WhenAgentControlEnabled()
        {
            Setup(true, "file://foo", 5);

            _agentHealthReporter.CollectMetrics();
            Assert.That(_publishedMetrics.Any(x => x.MetricNameModel.Name == "Supportability/AgentControl/Health/enabled"), Is.True);
        }

        [Test]
        public void AgentControl_HealthChecksSucceeded()
        {
            // Arrange
            // path must be absolute, not relative
            var executingPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)!;
            var readOnlyPath = Path.Combine(executingPath, Path.GetRandomFileName(), "readonly");

            var actualMS = new MemoryStream();
            FileStream fs = Mock.Create<FileStream>(Constructor.Mocked);
            Mock.Arrange(() => fs.Write(null, 0, 0)).IgnoreArguments()
                .DoInstead((byte[] content, int offset, int len) => actualMS.Write(content, 0, content.Length));
            Mock.Arrange(() => fs.CanWrite).Returns(true);

            var fileWrapper = Mock.Create<IFileWrapper>();
            Mock.Arrange(() => fileWrapper.TryCreateFile(Arg.IsAny<string>(), Arg.IsAny<bool>())).Returns(true);
            Mock.Arrange(() => fileWrapper.OpenWrite(Arg.IsAny<string>())).Returns(fs);

            var directoryWrapper = Mock.Create<IDirectoryWrapper>();
            Mock.Arrange(() => directoryWrapper.Exists(Arg.IsAny<string>())).Returns(true);

            Setup(true, $"file://{readOnlyPath}", 12, fileWrapper, directoryWrapper);

            // Act
            _agentHealthReporter.PublishAgentControlHealthCheck();

            // Assert
            // verify the health check hasn't failed
            Assert.That(_agentHealthReporter.HealthCheckFailed, Is.False);

            actualMS.Position = 0;
            var actualBytes = actualMS.ToArray();
            // convert actualBytes to a string
            var payload = Encoding.UTF8.GetString(actualBytes);

            var parsedObject = new SimpleYamlParser().ParseYaml(payload);
            Assert.That(parsedObject.healthy, Is.EqualTo("True"));
            Assert.That(parsedObject.status, Is.EqualTo("Agent starting"));
            Assert.That(parsedObject.last_error, Is.Empty);
            Assert.That(parsedObject.start_time_unix_nano, Is.Not.Empty);
            Assert.That(parsedObject.status_time_unix_nano, Is.Not.Empty);
        }

        [Test]
        public void AgentControl_PublishAgentControlHealthCheckScheduledTaskIsStopped_WhenAgentControlDisabled()
        {
            // Arrange
            Setup(false, "file://foo", 5);

            // Act
            _agentHealthReporter.PublishAgentControlHealthCheck();

            //Assert
            Mock.Assert(() => _scheduler.StopExecuting(_agentHealthReporter.PublishAgentControlHealthCheck, Arg.IsAny<TimeSpan?>()), Occurs.Once());
        }

        [Test]
        public void AgentControl_HealthChecksFailed_WhenDeliveryLocationDoesNotExist()
        {
            // Arrange
            // path must be absolute, not relative
            var executingPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)!;
            var readOnlyPath = Path.Combine(executingPath, Path.GetRandomFileName(), "readonly");

            var fileWrapper = Mock.Create<IFileWrapper>();

            var directoryWrapper = Mock.Create<IDirectoryWrapper>();
            Mock.Arrange(() => directoryWrapper.Exists(Arg.IsAny<string>())).Returns(false);

            Setup(true, $"file://{readOnlyPath}", 12, fileWrapper, directoryWrapper);

            // Act
            _agentHealthReporter.PublishAgentControlHealthCheck();

            // Assert
            Assert.That(_agentHealthReporter.HealthCheckFailed, Is.True);
        }

        [Test]
        public void AgentControl_HealthChecksFailed_WhenDeliveryLocationIsNotAFileURI()
        {
            // Arrange
            Setup(true, "http://foo", 12);

            // Act
            _agentHealthReporter.PublishAgentControlHealthCheck();

            // Assert
            Assert.That(_agentHealthReporter.HealthCheckFailed, Is.True);
        }

        [Test]
        public void AgentControl_HealthChecksFailed_WhenDeliveryLocationIsReadOnly()
        {
            // Arrange
            // path must be absolute, not relative
            var executingPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)!;
            var readOnlyPath = Path.Combine(executingPath, Path.GetRandomFileName(), "readonly");

            var fileWrapper = Mock.Create<IFileWrapper>();
            Mock.Arrange(() => fileWrapper.TryCreateFile(Arg.IsAny<string>(), Arg.IsAny<bool>())).Returns(false);

            var directoryWrapper = Mock.Create<IDirectoryWrapper>();
            Mock.Arrange(() => directoryWrapper.Exists(Arg.IsAny<string>())).Returns(true);

            Setup(true, $"file://{readOnlyPath}", 12, fileWrapper, directoryWrapper);

            // Act
            _agentHealthReporter.PublishAgentControlHealthCheck();

            // Assert
            // verify the health check failed
            Assert.That(_agentHealthReporter.HealthCheckFailed, Is.True);
        }

        [Test]
        public void AgentControl_HealthChecksFailed_WhenHealthFileCannotBeWritten()
        {
            // Arrange
            // path must be absolute, not relative
            var executingPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)!;
            var readOnlyPath = Path.Combine(executingPath, Path.GetRandomFileName(), "readonly");

            var fileWrapper = Mock.Create<IFileWrapper>();
            Mock.Arrange(() => fileWrapper.TryCreateFile(Arg.IsAny<string>(), Arg.IsAny<bool>())).Returns(true);
            Mock.Arrange(() => fileWrapper.OpenWrite(Arg.IsAny<string>())).Throws(new IOException());

            var directoryWrapper = Mock.Create<IDirectoryWrapper>();
            Mock.Arrange(() => directoryWrapper.Exists(Arg.IsAny<string>())).Returns(true);

            Setup(true, $"file://{readOnlyPath}", 12, fileWrapper, directoryWrapper);

            // Act
            _agentHealthReporter.PublishAgentControlHealthCheck();

            // Assert
            // verify the health check failed
            Assert.That(_agentHealthReporter.HealthCheckFailed, Is.True);
        }

        [Test]
        public void AgentControl_SetAgentControlStatus_SetsShutdownHealthy_WhenHealthCheckIsHealthy()
        {
            // Arrange
            // path must be absolute, not relative
            var executingPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)!;
            var readOnlyPath = Path.Combine(executingPath, Path.GetRandomFileName(), "readonly");

            var actualMS = new MemoryStream();
            FileStream fs = Mock.Create<FileStream>(Constructor.Mocked);
            Mock.Arrange(() => fs.Write(null, 0, 0)).IgnoreArguments()
                .DoInstead((byte[] content, int offset, int len) => actualMS.Write(content, 0, content.Length));
            Mock.Arrange(() => fs.CanWrite).Returns(true);

            var fileWrapper = Mock.Create<IFileWrapper>();
            Mock.Arrange(() => fileWrapper.TryCreateFile(Arg.IsAny<string>(), Arg.IsAny<bool>())).Returns(true);
            Mock.Arrange(() => fileWrapper.OpenWrite(Arg.IsAny<string>())).Returns(fs);

            var directoryWrapper = Mock.Create<IDirectoryWrapper>();
            Mock.Arrange(() => directoryWrapper.Exists(Arg.IsAny<string>())).Returns(true);

            Setup(true, $"file://{readOnlyPath}", 12, fileWrapper, directoryWrapper);

            // Act
            _agentHealthReporter.SetAgentControlStatus(HealthCodes.AgentShutdownHealthy);
            _agentHealthReporter.PublishAgentControlHealthCheck();

            // Assert
            // verify the health check hasn't failed
            Assert.That(_agentHealthReporter.HealthCheckFailed, Is.False);

            actualMS.Position = 0;
            var actualBytes = actualMS.ToArray();
            // convert actualBytes to a string
            var payload = Encoding.UTF8.GetString(actualBytes);

            var parsedObject = new SimpleYamlParser().ParseYaml(payload);
            Assert.That(parsedObject.healthy, Is.EqualTo("True"));
            Assert.That(parsedObject.status, Is.EqualTo("Agent has shutdown"));
            Assert.That(parsedObject.last_error, Is.EqualTo("NR-APM-099"));

        }

        [Test]
        public void AgentControl_SetAgentControlStatus_DoesNotOverwriteLastStatusOnHealthyShutdown_WhenAgentIsNotHealthy()
        {
            // Arrange
            // path must be absolute, not relative
            var executingPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)!;
            var readOnlyPath = Path.Combine(executingPath, Path.GetRandomFileName(), "readonly");

            var actualMS = new MemoryStream();
            FileStream fs = Mock.Create<FileStream>(Constructor.Mocked);
            Mock.Arrange(() => fs.Write(null, 0, 0)).IgnoreArguments()
                .DoInstead((byte[] content, int offset, int len) => actualMS.Write(content, 0, content.Length));
            Mock.Arrange(() => fs.CanWrite).Returns(true);

            var fileWrapper = Mock.Create<IFileWrapper>();
            Mock.Arrange(() => fileWrapper.TryCreateFile(Arg.IsAny<string>(), Arg.IsAny<bool>())).Returns(true);
            Mock.Arrange(() => fileWrapper.OpenWrite(Arg.IsAny<string>())).Returns(fs);

            var directoryWrapper = Mock.Create<IDirectoryWrapper>();
            Mock.Arrange(() => directoryWrapper.Exists(Arg.IsAny<string>())).Returns(true);

            Setup(true, $"file://{readOnlyPath}", 12, fileWrapper, directoryWrapper);

            // Act
            _agentHealthReporter.SetAgentControlStatus(HealthCodes.FailedToConnect);
            _agentHealthReporter.PublishAgentControlHealthCheck();
            _agentHealthReporter.SetAgentControlStatus(HealthCodes.AgentShutdownHealthy);
            _agentHealthReporter.PublishAgentControlHealthCheck();

            // Assert
            // verify the health check hasn't failed
            Assert.That(_agentHealthReporter.HealthCheckFailed, Is.False);

            actualMS.Position = 0;
            var actualBytes = actualMS.ToArray();
            // convert actualBytes to a string
            var payload = Encoding.UTF8.GetString(actualBytes);

            var parsedObject = new SimpleYamlParser().ParseYaml(payload);
            Assert.That(parsedObject.healthy, Is.EqualTo("False"));
            Assert.That(parsedObject.status, Is.EqualTo("Failed to connect to New Relic data collector"));
            Assert.That(parsedObject.last_error, Is.EqualTo("NR-APM-009"));

        }

    }

    public class SimpleYamlParser
    {
        public dynamic ParseYaml(string yamlContent)
        {
            var result = new ExpandoObject() as IDictionary<string, object>;
            using (var reader = new StringReader(yamlContent))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    var parts = line.Split(new[] { ':' }, 2);
                    if (parts.Length == 2)
                    {
                        var key = parts[0].Trim();
                        var value = parts[1].Trim();
                        result[key] = value;
                    }
                }
            }

            return result;
        }
    }
}
