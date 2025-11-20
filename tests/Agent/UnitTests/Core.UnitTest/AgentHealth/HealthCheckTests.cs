// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NUnit.Framework;
using NewRelic.Agent.Core.AgentHealth;
using NUnit.Framework.Legacy;

namespace NewRelic.Agent.Core.UnitTests.AgentHealth
{
    [TestFixture]
    public class HealthCheckTests
    {
        private HealthCheck _healthCheck;

        [SetUp]
        public void SetUp()
        {
            _healthCheck = new HealthCheck();
        }

        [Test]
        public void HealthCheck_InitialValues_AreCorrect()
        {
            Assert.Multiple(() =>
            {
                Assert.That(_healthCheck.IsHealthy, Is.False);
                Assert.That(_healthCheck.Status, Is.Null);
                Assert.That(_healthCheck.LastError, Is.Null);
                Assert.That(_healthCheck.StartTime.Date, Is.EqualTo(DateTime.UtcNow.Date));
                Assert.That(_healthCheck.StatusTime, Is.EqualTo(default(DateTime)));
                Assert.That(_healthCheck.FileName, Does.StartWith("health-"));
                Assert.That(_healthCheck.FileName, Does.EndWith(".yml"));
            });
        }

        [Test]
        public void TrySetHealth_UpdatesValuesCorrectly()
        {
            var healthStatus = (IsHealthy: true, Code: "200", Status: "OK");

            _healthCheck.TrySetHealth(healthStatus);

            Assert.Multiple(() =>
            {
                Assert.That(_healthCheck.IsHealthy, Is.True);
                Assert.That(_healthCheck.Status, Is.EqualTo("OK"));
                Assert.That(_healthCheck.LastError, Is.EqualTo("200"));
            });
        }

        [Test]
        public void TrySetHealth_DoesNotUpdateUnchangedValues()
        {
            var initialStatus = (IsHealthy: false, Code: "500", Status: "Error");
            _healthCheck.TrySetHealth(initialStatus);

            var newStatus = (IsHealthy: false, Code: "500", Status: "Error");
            _healthCheck.TrySetHealth(newStatus);

            Assert.Multiple(() =>
            {
                Assert.That(_healthCheck.IsHealthy, Is.False);
                Assert.That(_healthCheck.Status, Is.EqualTo("Error"));
                Assert.That(_healthCheck.LastError, Is.EqualTo("500"));
            });
        }

        [Test]
        public void ToYaml_ReturnsCorrectYamlString()
        {
            var healthStatus = (IsHealthy: true, Code: "200", Status: "OK");
            _healthCheck.TrySetHealth(healthStatus);

            var yaml = _healthCheck.ToYaml("someEntityGuid");

            Assert.Multiple(() =>
            {
                Assert.That(yaml, Does.Contain("entity_guid: someEntityGuid"));
                Assert.That(yaml, Does.Contain("healthy: True"));
                Assert.That(yaml, Does.Contain("status: OK"));
                Assert.That(yaml, Does.Contain("last_error: 200"));
                Assert.That(yaml, Does.Contain("start_time_unix_nano:"));
                Assert.That(yaml, Does.Contain("status_time_unix_nano:"));
            });
        }

        [Test]
        public void TrySetHealth_UpdatesStatusTime()
        {
            var healthStatus = (IsHealthy: true, Code: "200", Status: "OK");

            _healthCheck.TrySetHealth(healthStatus);

            Assert.That(_healthCheck.StatusTime, Is.Not.EqualTo(default(DateTime)));
        }

        [Test]
        public void TrySetHealth_HandlesNullStatus()
        {
            (bool IsHealthy, string Code, string Status) healthStatus = (IsHealthy: true, Code: "200", Status: null);

            _healthCheck.TrySetHealth(healthStatus);

            Assert.Multiple(() =>
            {
                Assert.That(_healthCheck.IsHealthy, Is.True);
                Assert.That(_healthCheck.Status, Is.Null);
                Assert.That(_healthCheck.LastError, Is.EqualTo("200"));
            });
        }

        [Test]
        public void TrySetHealth_HandlesEmptyStatus()
        {
            var healthStatus = (IsHealthy: true, Code: "200", Status: string.Empty);

            _healthCheck.TrySetHealth(healthStatus);

            Assert.Multiple(() =>
            {
                Assert.That(_healthCheck.IsHealthy, Is.True);
                Assert.That(_healthCheck.Status, Is.Empty);
                Assert.That(_healthCheck.LastError, Is.EqualTo("200"));
            });
        }

        [Test]
        public void TrySetHealth_HandlesNullCode()
        {
            var healthStatus = (IsHealthy: true, Code: (string)null, Status: "OK");

            _healthCheck.TrySetHealth(healthStatus);

            Assert.Multiple(() =>
            {
                Assert.That(_healthCheck.IsHealthy, Is.True);
                Assert.That(_healthCheck.Status, Is.EqualTo("OK"));
                Assert.That(_healthCheck.LastError, Is.Null);
            });
        }

        [Test]
        public void TrySetHealth_HandlesEmptyCode()
        {
            var healthStatus = (IsHealthy: true, Code: string.Empty, Status: "OK");

            _healthCheck.TrySetHealth(healthStatus);

            Assert.Multiple(() =>
            {
                Assert.That(_healthCheck.IsHealthy, Is.True);
                Assert.That(_healthCheck.Status, Is.EqualTo("OK"));
                Assert.That(_healthCheck.LastError, Is.Empty);
            });
        }

        [Test]
        public void TrySetHealth_HandlesFalseIsHealthy()
        {
            var healthStatus = (IsHealthy: false, Code: "500", Status: "Error");

            _healthCheck.TrySetHealth(healthStatus);

            Assert.Multiple(() =>
            {
                Assert.That(_healthCheck.IsHealthy, Is.False);
                Assert.That(_healthCheck.Status, Is.EqualTo("Error"));
                Assert.That(_healthCheck.LastError, Is.EqualTo("500"));
            });
        }

        [Test]
        public void ToYaml_HandlesNullValues()
        {
            var yaml = _healthCheck.ToYaml(null);

            Assert.Multiple(() =>
            {
                Assert.That(yaml, Does.Match(@"entity_guid:\s\n"));
                Assert.That(yaml, Does.Match(@"healthy:\sFalse"));
                Assert.That(yaml, Does.Match(@"status:\s\n"));
                Assert.That(yaml, Does.Match(@"last_error:\s\n"));
                Assert.That(yaml, Does.Match(@"start_time_unix_nano:\s\d+"));
                Assert.That(yaml, Does.Match(@"status_time_unix_nano:\s-?\d+"));
            });
        }

        [Test]
        public void TrySetHealth_Healthy()
        {
            _healthCheck.TrySetHealth(HealthCodes.Healthy);

            Assert.Multiple(() =>
            {
                Assert.That(_healthCheck.IsHealthy, Is.True);
                Assert.That(_healthCheck.Status, Is.EqualTo("Healthy"));
                Assert.That(_healthCheck.LastError, Is.EqualTo("NR-APM-000"));
                Assert.That(_healthCheck.StatusTime, Is.EqualTo(DateTime.UtcNow).Within(TimeSpan.FromSeconds(5)));
            });
        }

        [Test]
        public void TrySetHealth_LicenseKeyInvalid()
        {
            _healthCheck.TrySetHealth(HealthCodes.LicenseKeyInvalid);

            Assert.Multiple(() =>
            {
                Assert.That(_healthCheck.IsHealthy, Is.False);
                Assert.That(_healthCheck.Status, Is.EqualTo("Invalid license key (HTTP status code 401)"));
                Assert.That(_healthCheck.LastError, Is.EqualTo("NR-APM-001"));
                Assert.That(_healthCheck.StatusTime, Is.EqualTo(DateTime.UtcNow).Within(TimeSpan.FromSeconds(5)));
            });
        }

        [Test]
        public void TrySetHealth_LicenseKeyMissing()
        {
            _healthCheck.TrySetHealth(HealthCodes.LicenseKeyMissing);

            Assert.Multiple(() =>
            {
                Assert.That(_healthCheck.IsHealthy, Is.False);
                Assert.That(_healthCheck.Status, Is.EqualTo("License key missing in configuration"));
                Assert.That(_healthCheck.LastError, Is.EqualTo("NR-APM-002"));
                Assert.That(_healthCheck.StatusTime, Is.EqualTo(DateTime.UtcNow).Within(TimeSpan.FromSeconds(5)));
            });
        }

        [Test]
        public void TrySetHealth_ForceDisconnect()
        {
            _healthCheck.TrySetHealth(HealthCodes.ForceDisconnect);

            Assert.Multiple(() =>
            {
                Assert.That(_healthCheck.IsHealthy, Is.False);
                Assert.That(_healthCheck.Status, Is.EqualTo("Forced disconnect received from New Relic (HTTP status code 410)"));
                Assert.That(_healthCheck.LastError, Is.EqualTo("NR-APM-003"));
                Assert.That(_healthCheck.StatusTime, Is.EqualTo(DateTime.UtcNow).Within(TimeSpan.FromSeconds(5)));
            });
        }

        [Test]
        public void TrySetHealth_HttpError()
        {
            _healthCheck.TrySetHealth(HealthCodes.HttpError, "500", "metric");

            Assert.Multiple(() =>
            {
                Assert.That(_healthCheck.IsHealthy, Is.False);
                Assert.That(_healthCheck.Status, Is.EqualTo("HTTP error response code 500 received from New Relic while sending data type metric"));
                Assert.That(_healthCheck.LastError, Is.EqualTo("NR-APM-004"));
                Assert.That(_healthCheck.StatusTime, Is.EqualTo(DateTime.UtcNow).Within(TimeSpan.FromSeconds(5)));
            });
        }

        [Test]
        public void TrySetHealth_ApplicationNameMissing()
        {
            _healthCheck.TrySetHealth(HealthCodes.ApplicationNameMissing);

            Assert.Multiple(() =>
            {
                Assert.That(_healthCheck.IsHealthy, Is.False);
                Assert.That(_healthCheck.Status, Is.EqualTo("Missing application name in agent configuration"));
                Assert.That(_healthCheck.LastError, Is.EqualTo("NR-APM-005"));
                Assert.That(_healthCheck.StatusTime, Is.EqualTo(DateTime.UtcNow).Within(TimeSpan.FromSeconds(5)));
            });
        }

        [Test]
        public void TrySetHealth_MaxApplicationNamesExceeded()
        {
            _healthCheck.TrySetHealth(HealthCodes.MaxApplicationNamesExceeded);

            Assert.Multiple(() =>
            {
                Assert.That(_healthCheck.IsHealthy, Is.False);
                Assert.That(_healthCheck.Status, Is.EqualTo("The maximum number of configured app names (3) exceeded"));
                Assert.That(_healthCheck.LastError, Is.EqualTo("NR-APM-006"));
                Assert.That(_healthCheck.StatusTime, Is.EqualTo(DateTime.UtcNow).Within(TimeSpan.FromSeconds(5)));
            });
        }

        [Test]
        public void TrySetHealth_HttpProxyError()
        {
            _healthCheck.TrySetHealth(HealthCodes.HttpProxyError, "407");

            Assert.Multiple(() =>
            {
                Assert.That(_healthCheck.IsHealthy, Is.False);
                Assert.That(_healthCheck.Status, Is.EqualTo("HTTP Proxy configuration error; response code 407"));
                Assert.That(_healthCheck.LastError, Is.EqualTo("NR-APM-007"));
                Assert.That(_healthCheck.StatusTime, Is.EqualTo(DateTime.UtcNow).Within(TimeSpan.FromSeconds(5)));
            });
        }

        [Test]
        public void TrySetHealth_AgentDisabledByConfiguration()
        {
            _healthCheck.TrySetHealth(HealthCodes.AgentDisabledByConfiguration);

            Assert.Multiple(() =>
            {
                Assert.That(_healthCheck.IsHealthy, Is.False);
                Assert.That(_healthCheck.Status, Is.EqualTo("Agent is disabled via configuration"));
                Assert.That(_healthCheck.LastError, Is.EqualTo("NR-APM-008"));
                Assert.That(_healthCheck.StatusTime, Is.EqualTo(DateTime.UtcNow).Within(TimeSpan.FromSeconds(5)));
            });
        }

        [Test]
        public void TrySetHealth_FailedToConnect()
        {
            _healthCheck.TrySetHealth(HealthCodes.FailedToConnect);

            Assert.Multiple(() =>
            {
                Assert.That(_healthCheck.IsHealthy, Is.False);
                Assert.That(_healthCheck.Status, Is.EqualTo("Failed to connect to New Relic data collector"));
                Assert.That(_healthCheck.LastError, Is.EqualTo("NR-APM-009"));
                Assert.That(_healthCheck.StatusTime, Is.EqualTo(DateTime.UtcNow).Within(TimeSpan.FromSeconds(5)));
            });
        }

        [Test]
        public void TrySetHealth_AgentShutdownHealthy()
        {
            _healthCheck.TrySetHealth(HealthCodes.AgentShutdownHealthy);

            Assert.Multiple(() =>
            {
                Assert.That(_healthCheck.IsHealthy, Is.True);
                Assert.That(_healthCheck.Status, Is.EqualTo("Agent has shutdown"));
                Assert.That(_healthCheck.LastError, Is.EqualTo("NR-APM-099"));
                Assert.That(_healthCheck.StatusTime, Is.EqualTo(DateTime.UtcNow).Within(TimeSpan.FromSeconds(5)));
            });
        }

        [Test]
        public void TrySetHealth_AgentShutdownError()
        {
            _healthCheck.TrySetHealth(HealthCodes.AgentShutdownError, "Exception message");

            Assert.Multiple(() =>
            {
                Assert.That(_healthCheck.IsHealthy, Is.False);
                Assert.That(_healthCheck.Status, Is.EqualTo("Agent has shutdown with exception Exception message"));
                Assert.That(_healthCheck.LastError, Is.EqualTo("NR-APM-200"));
                Assert.That(_healthCheck.StatusTime, Is.EqualTo(DateTime.UtcNow).Within(TimeSpan.FromSeconds(5)));
            });
        }
    }
}
