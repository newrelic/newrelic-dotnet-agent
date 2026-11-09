// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using NewRelic.Agent.Api;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.DataTransport;
using NewRelic.Agent.Core.Events;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Extensions.Logging;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.OtelDistro;

[TestFixture]
public class DistroMetricsSettingsTests
{
    private IAgent _mockAgent;
    private IConfiguration _mockConfig;
    private IConnectionInfo _mockConnectionInfo;
    private DistroMetricsSettings _settings;
    private List<string> _warnings;
    private bool _logInitialized;

    [SetUp]
    public void SetUp()
    {
        _mockAgent = Mock.Create<IAgent>();
        _mockConfig = Mock.Create<IConfiguration>();
        Mock.Arrange(() => _mockAgent.Configuration).Returns(_mockConfig);

        Mock.Arrange(() => _mockConfig.OpenTelemetryMetricsEnabled).Returns(true);
        Mock.Arrange(() => _mockConfig.AgentLicenseKey).Returns("test-license-key");
        Mock.Arrange(() => _mockConfig.ApplicationNames).Returns(new[] { "MyApp", "SecondName" });
        Mock.Arrange(() => _mockConfig.EntityGuid).Returns("guid-1");
        Mock.Arrange(() => _mockConfig.OtlpResourceAttributes).Returns(new Dictionary<string, string> { { "server.attr", "1" } });
        Mock.Arrange(() => _mockConfig.OpenTelemetryMetricsExportIntervalMs).Returns(60000);
        Mock.Arrange(() => _mockConfig.OpenTelemetryMetricsExportTimeoutMs).Returns(10000);
        Mock.Arrange(() => _mockConfig.OpenTelemetryMetricsIncludeFilters).Returns(new string[0]);
        Mock.Arrange(() => _mockConfig.OpenTelemetryMetricsExcludeFilters).Returns(new string[0]);

        _mockConnectionInfo = Mock.Create<IConnectionInfo>();
        Mock.Arrange(() => _mockConnectionInfo.Host).Returns("collector.example.com");
        Mock.Arrange(() => _mockConnectionInfo.Port).Returns(443);
        Mock.Arrange(() => _mockConnectionInfo.HttpProtocol).Returns("https");
        Mock.Arrange(() => _mockConnectionInfo.Proxy).Returns((WebProxy)null);

        _warnings = new List<string>();
        var logger = Mock.Create<ILogger>();
        Mock.Arrange(() => logger.Warn(Arg.IsAny<Exception>(), Arg.IsAny<string>(), Arg.IsAny<object[]>()))
            .DoInstead((Exception exception, string message, object[] args) => _warnings.Add(message));
        Log.Initialize(logger);
        _logInitialized = true;

        _settings = null;
    }

    [TearDown]
    public void TearDown()
    {
        _settings?.Disable();

        if (_logInitialized)
        {
            Log.Initialize(new NoOpLogger());
        }
    }

    private static IConnectionInfo NewConnectionInfo(string host = "collector.example.com", int port = 443, string protocol = "https", WebProxy proxy = null)
    {
        var connectionInfo = Mock.Create<IConnectionInfo>();
        Mock.Arrange(() => connectionInfo.Host).Returns(host);
        Mock.Arrange(() => connectionInfo.Port).Returns(port);
        Mock.Arrange(() => connectionInfo.HttpProtocol).Returns(protocol);
        Mock.Arrange(() => connectionInfo.Proxy).Returns(proxy);
        return connectionInfo;
    }

    private void Connect(IConnectionInfo connectionInfo = null)
    {
        EventBus<AgentConnectedEvent>.Publish(new AgentConnectedEvent { ConnectInfo = connectionInfo ?? _mockConnectionInfo });
    }

    [Test]
    public void Ctor_NullAgent_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new DistroMetricsSettings(null));
    }

    [TestCase(true, false, true)]
    [TestCase(false, false, false)]
    [TestCase(true, true, false)]
    public void Enabled_ReflectsMetricsEnabledAndDisable(bool metricsEnabled, bool disabled, bool expected)
    {
        Mock.Arrange(() => _mockConfig.OpenTelemetryMetricsEnabled).Returns(metricsEnabled);
        _settings = new DistroMetricsSettings(_mockAgent);

        if (disabled)
        {
            _settings.Disable();
        }

        Assert.That(_settings.Enabled, Is.EqualTo(expected));
    }

    [Test]
    public void ShouldEnableMeter_DelegatesToMeterFilterHelpers()
    {
        Mock.Arrange(() => _mockConfig.OpenTelemetryMetricsExcludeFilters).Returns(new[] { "My.Custom.Meter" });
        _settings = new DistroMetricsSettings(_mockAgent);

        Assert.Multiple(() =>
        {
            Assert.That(_settings.ShouldEnableMeter("My.Custom.Meter"), Is.False);
            Assert.That(_settings.ShouldEnableMeter("OpenTelemetry.Instrumentation.Http"), Is.False);
        });
    }

    [Test]
    public void MeterPredicate_IncludeOverridesBuiltInExclusion()
    {
        Mock.Arrange(() => _mockConfig.OpenTelemetryMetricsIncludeFilters).Returns(new[] { "OpenTelemetry.Instrumentation.AspNet" });
        _settings = new DistroMetricsSettings(_mockAgent);

        Assert.That(_settings.ShouldEnableMeter("OpenTelemetry.Instrumentation.AspNet"), Is.True);
    }

    [Test]
    public void BeforeConnect_EndpointAndResourceAreNull()
    {
        _settings = new DistroMetricsSettings(_mockAgent);

        Assert.Multiple(() =>
        {
            Assert.That(_settings.Endpoint, Is.Null);
            Assert.That(_settings.ResourceAttributes, Is.Null);
            Assert.That(_settings.IsConnected, Is.False);
            Assert.That(_settings.ServiceName, Is.Null);
        });
    }

    [Test]
    public void AfterConnect_BuildsEndpointHeadersResourceIntervalTimeout()
    {
        _settings = new DistroMetricsSettings(_mockAgent);

        Connect();

        Assert.Multiple(() =>
        {
            Assert.That(_settings.IsConnected, Is.True);
            Assert.That(_settings.Endpoint, Is.EqualTo(new Uri("https://collector.example.com:443/v1/metrics")));
            Assert.That(_settings.ServiceName, Is.EqualTo("MyApp"));
            Assert.That(_settings.Headers, Is.EqualTo("api-key=test-license-key"));
            Assert.That(_settings.ExportIntervalMs, Is.EqualTo(60000));
            Assert.That(_settings.ExportTimeoutMs, Is.EqualTo(10000));

            var attributes = _settings.ResourceAttributes.ToDictionary(p => p.Key, p => p.Value);
            Assert.That(attributes["entity.guid"], Is.EqualTo("guid-1"));
            Assert.That(attributes["server.attr"], Is.EqualTo("1"));
            Assert.That(attributes.ContainsKey("service.name"), Is.False);
        });
    }

    [Test]
    public void AgentConnectedEvent_NullConnectInfo_DoesNotConnect()
    {
        _settings = new DistroMetricsSettings(_mockAgent);

        Assert.DoesNotThrow(() => EventBus<AgentConnectedEvent>.Publish(new AgentConnectedEvent { ConnectInfo = null }));

        Assert.That(_settings.IsConnected, Is.False);
    }

    [Test]
    public void ApplicationNames_Null_ServiceNameIsNullAndDoesNotThrow()
    {
        Mock.Arrange(() => _mockConfig.ApplicationNames).Returns((IEnumerable<string>)null);
        _settings = new DistroMetricsSettings(_mockAgent);

        Assert.DoesNotThrow(() => Connect());

        Assert.Multiple(() =>
        {
            Assert.That(_settings.IsConnected, Is.True);
            Assert.That(_settings.ServiceName, Is.Null);
        });
    }

    [Test]
    public void ServerConfigUpdate_BeforeConnect_DoesNothing()
    {
        _settings = new DistroMetricsSettings(_mockAgent);
        var changedCount = 0;
        _settings.Changed += () => changedCount++;

        Assert.DoesNotThrow(() => EventBus<ServerConfigurationUpdatedEvent>.Publish(new ServerConfigurationUpdatedEvent(null)));

        Assert.Multiple(() =>
        {
            Assert.That(_settings.IsConnected, Is.False);
            Assert.That(changedCount, Is.EqualTo(0));
        });
    }

    [Test]
    public void EntityGuidArrivesAfterConnect_RaisesChangedOnce()
    {
        Mock.Arrange(() => _mockConfig.EntityGuid).Returns((string)null);
        _settings = new DistroMetricsSettings(_mockAgent);
        Connect();

        var changedCount = 0;
        _settings.Changed += () => changedCount++;

        Mock.Arrange(() => _mockConfig.EntityGuid).Returns("guid-2");
        EventBus<ServerConfigurationUpdatedEvent>.Publish(new ServerConfigurationUpdatedEvent(null));

        Assert.That(changedCount, Is.EqualTo(1));
    }

    [Test]
    public void ServerConfigUpdate_WithNoMaterialChange_DoesNotRaiseChanged()
    {
        _settings = new DistroMetricsSettings(_mockAgent);
        Connect();

        var changedCount = 0;
        _settings.Changed += () => changedCount++;

        EventBus<ServerConfigurationUpdatedEvent>.Publish(new ServerConfigurationUpdatedEvent(null));

        Assert.That(changedCount, Is.EqualTo(0));
    }

    [Test]
    public void ServerConfigUpdate_WithResourceAttrChange_RaisesChanged()
    {
        _settings = new DistroMetricsSettings(_mockAgent);
        Connect();

        var changedCount = 0;
        _settings.Changed += () => changedCount++;

        Mock.Arrange(() => _mockConfig.OtlpResourceAttributes).Returns(new Dictionary<string, string> { { "server.attr", "2" } });
        EventBus<ServerConfigurationUpdatedEvent>.Publish(new ServerConfigurationUpdatedEvent(null));

        Assert.That(changedCount, Is.EqualTo(1));
    }

    [Test]
    public void ServerConfigUpdate_KeyMissingSameCount_RaisesChanged()
    {
        _settings = new DistroMetricsSettings(_mockAgent);
        Connect();

        var changedCount = 0;
        _settings.Changed += () => changedCount++;

        Mock.Arrange(() => _mockConfig.OtlpResourceAttributes).Returns(new Dictionary<string, string> { { "different.key", "1" } });
        EventBus<ServerConfigurationUpdatedEvent>.Publish(new ServerConfigurationUpdatedEvent(null));

        Assert.That(changedCount, Is.EqualTo(1));
    }

    [Test]
    public void ServerConfigUpdate_CountDiffers_RaisesChanged()
    {
        _settings = new DistroMetricsSettings(_mockAgent);
        Connect();

        var changedCount = 0;
        _settings.Changed += () => changedCount++;

        Mock.Arrange(() => _mockConfig.OtlpResourceAttributes).Returns(new Dictionary<string, string> { { "server.attr", "1" }, { "extra", "x" } });
        EventBus<ServerConfigurationUpdatedEvent>.Publish(new ServerConfigurationUpdatedEvent(null));

        Assert.That(changedCount, Is.EqualTo(1));
    }

    [Test]
    public void ServerConfigUpdate_NullVsEmptyResourceAttributes_DoesNotRaiseChanged()
    {
        Mock.Arrange(() => _mockConfig.OtlpResourceAttributes).Returns((IReadOnlyDictionary<string, string>)null);
        _settings = new DistroMetricsSettings(_mockAgent);
        Connect();

        var changedCount = 0;
        _settings.Changed += () => changedCount++;

        Mock.Arrange(() => _mockConfig.OtlpResourceAttributes).Returns(new Dictionary<string, string>());
        EventBus<ServerConfigurationUpdatedEvent>.Publish(new ServerConfigurationUpdatedEvent(null));

        Assert.That(changedCount, Is.EqualTo(0));
    }

    [Test]
    public void Reconnect_WithSameValues_DoesNotRaiseChanged()
    {
        _settings = new DistroMetricsSettings(_mockAgent);
        Connect();

        var changedCount = 0;
        _settings.Changed += () => changedCount++;

        // A distinct IConnectionInfo instance with the same Host/Port/HttpProtocol -- exercises the value-based
        // comparison branch of ConnectionInfoEquals, not its ReferenceEquals shortcut.
        Connect(NewConnectionInfo());

        Assert.That(changedCount, Is.EqualTo(0));
    }

    [Test]
    public void Reconnect_WithSameConnectionInfoInstance_DoesNotRaiseChanged()
    {
        _settings = new DistroMetricsSettings(_mockAgent);
        Connect();

        var changedCount = 0;
        _settings.Changed += () => changedCount++;

        // The exact same IConnectionInfo instance as the first connect -- exercises ConnectionInfoEquals'
        // ReferenceEquals shortcut.
        Connect(_mockConnectionInfo);

        Assert.That(changedCount, Is.EqualTo(0));
    }

    [Test]
    public void Reconnect_WithHostChange_RaisesChanged()
    {
        _settings = new DistroMetricsSettings(_mockAgent);
        Connect();

        var changedCount = 0;
        _settings.Changed += () => changedCount++;

        Connect(NewConnectionInfo(host: "new-collector.example.com"));

        Assert.Multiple(() =>
        {
            Assert.That(changedCount, Is.EqualTo(1));
            Assert.That(_settings.Endpoint, Is.EqualTo(new Uri("https://new-collector.example.com:443/v1/metrics")));
        });
    }

    [Test]
    public void OnConnected_ConfigurationThrows_DoesNotThrowAndLogsWarning()
    {
        Mock.Arrange(() => _mockConfig.EntityGuid).Throws(new InvalidOperationException("config boom"));
        _settings = new DistroMetricsSettings(_mockAgent);

        Assert.DoesNotThrow(() => Connect());

        Assert.Multiple(() =>
        {
            Assert.That(_settings.IsConnected, Is.False);
            Assert.That(_warnings, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public void OnServerConfigurationUpdated_ConfigurationThrows_DoesNotThrowAndLogsWarning()
    {
        _settings = new DistroMetricsSettings(_mockAgent);
        Connect();

        Mock.Arrange(() => _mockConfig.OtlpResourceAttributes).Throws(new InvalidOperationException("config boom"));

        Assert.DoesNotThrow(() => EventBus<ServerConfigurationUpdatedEvent>.Publish(new ServerConfigurationUpdatedEvent(null)));

        Assert.That(_warnings, Has.Count.EqualTo(1));
    }

    [Test]
    public void HttpClient_ChainOrderAndTimeout()
    {
        var proxy = new WebProxy("http://proxy.example.com:8080");
        Mock.Arrange(() => _mockConnectionInfo.Proxy).Returns(proxy);
        _settings = new DistroMetricsSettings(_mockAgent);
        Connect();

        var httpClient = _settings.CreateHttpClient();
        var handler = _settings.BuildHttpMessageHandlerChain();

        Assert.Multiple(() =>
        {
            Assert.That(handler, Is.InstanceOf<OtlpAuditHandler>());
            var auditHandler = (DelegatingHandler)handler;
            Assert.That(auditHandler.InnerHandler, Is.InstanceOf<CustomRetryHandler>());
            var retryHandler = (DelegatingHandler)auditHandler.InnerHandler;
            Assert.That(retryHandler.InnerHandler, Is.InstanceOf<HttpClientHandler>());
            var httpClientHandler = (HttpClientHandler)retryHandler.InnerHandler;
            Assert.That(httpClientHandler.Proxy, Is.SameAs(proxy));
            Assert.That(httpClientHandler.UseProxy, Is.True);
            Assert.That(httpClient.Timeout, Is.EqualTo(TimeSpan.FromMilliseconds(10000)));
        });
    }

    // The metrics plugin injects a sync-to-async bridge here; prove it becomes the outermost handler while the
    // audit -> retry -> transport chain stays intact below it and the client keeps the export timeout.
    [Test]
    public void CreateHttpClient_WithOutermostHandler_WrapsChainOutermost()
    {
        _settings = new DistroMetricsSettings(_mockAgent);
        Connect();

        var outermost = new PassthroughHandler();
        var httpClient = _settings.CreateHttpClient(outermost);

        Assert.Multiple(() =>
        {
            Assert.That(outermost.InnerHandler, Is.InstanceOf<OtlpAuditHandler>());
            var auditHandler = (DelegatingHandler)outermost.InnerHandler;
            Assert.That(auditHandler.InnerHandler, Is.InstanceOf<CustomRetryHandler>());
            var retryHandler = (DelegatingHandler)auditHandler.InnerHandler;
            Assert.That(retryHandler.InnerHandler, Is.InstanceOf<HttpClientHandler>());
            Assert.That(httpClient.Timeout, Is.EqualTo(TimeSpan.FromMilliseconds(10000)));
        });
    }

    private sealed class PassthroughHandler : DelegatingHandler
    {
    }

    [Test]
    public void HttpClient_NoProxy_DoesNotUseProxy()
    {
        _settings = new DistroMetricsSettings(_mockAgent);
        Connect();

        var handler = (DelegatingHandler)_settings.BuildHttpMessageHandlerChain();
        var retryHandler = (DelegatingHandler)handler.InnerHandler;
        var httpClientHandler = (HttpClientHandler)retryHandler.InnerHandler;

        Assert.That(httpClientHandler.UseProxy, Is.False);
    }

    // ExportTimeoutMs is 0 before connect (_snapshot?.ExportTimeoutMs ?? 0), and HttpClient.Timeout's setter
    // throws ArgumentOutOfRangeException for any TimeSpan <= 0 other than Timeout.InfiniteTimeSpan -- so
    // CreateHttpClient's catch block is reachable through ordinary use, not just a defensive-only guard.
    [Test]
    public void CreateHttpClient_BeforeConnect_ReturnsFallbackClientWithoutThrowing()
    {
        _settings = new DistroMetricsSettings(_mockAgent);

        HttpClient httpClient = null;
        Assert.DoesNotThrow(() => httpClient = _settings.CreateHttpClient());

        Assert.Multiple(() =>
        {
            Assert.That(httpClient, Is.Not.Null);
            // The fallback is a bare new HttpClient(), so it keeps the framework default 100s timeout rather than
            // the export timeout -- pins that this is the catch-block client, not a normally built one.
            Assert.That(httpClient.Timeout, Is.EqualTo(TimeSpan.FromSeconds(100)));
        });
    }

    [Test]
    public void Shutdown_BeforeConnect_DoesNotThrow()
    {
        _settings = new DistroMetricsSettings(_mockAgent);

        Assert.DoesNotThrow(() => _settings.Shutdown(flush: true));
    }

    [Test]
    public void Shutdown_RunsRegisteredCallbackOnce()
    {
        _settings = new DistroMetricsSettings(_mockAgent);
        var callCount = 0;
        _settings.RegisterShutdown(_ => callCount++);

        _settings.Shutdown(flush: true);

        Assert.That(callCount, Is.EqualTo(1));
    }

    [TestCase(true)]
    [TestCase(false)]
    public void Shutdown_ForwardsFlushArgumentToCallback(bool flush)
    {
        _settings = new DistroMetricsSettings(_mockAgent);
        bool? captured = null;
        _settings.RegisterShutdown(f => captured = f);

        _settings.Shutdown(flush);

        Assert.That(captured, Is.EqualTo(flush));
    }

    [Test]
    public void Shutdown_CalledTwice_InvokesCallbackOnlyOnce()
    {
        _settings = new DistroMetricsSettings(_mockAgent);
        var callCount = 0;
        _settings.RegisterShutdown(_ => callCount++);

        _settings.Shutdown(flush: true);
        _settings.Shutdown(flush: false);

        Assert.That(callCount, Is.EqualTo(1));
    }

    [Test]
    public void Shutdown_CallbackThrows_DoesNotThrowAndLogsWarning()
    {
        _settings = new DistroMetricsSettings(_mockAgent);
        _settings.RegisterShutdown(_ => throw new InvalidOperationException("teardown boom"));

        Assert.DoesNotThrow(() => _settings.Shutdown(flush: true));

        Assert.That(_warnings, Has.Count.EqualTo(1));
    }

    [Test]
    public void Shutdown_AfterDisable_StillRunsCallback()
    {
        _settings = new DistroMetricsSettings(_mockAgent);
        var callCount = 0;
        _settings.RegisterShutdown(_ => callCount++);

        _settings.Disable();
        _settings.Shutdown(flush: true);

        Assert.That(callCount, Is.EqualTo(1));
    }

    [Test]
    public void Disable_StopsChanged_AndUnsubscribes()
    {
        _settings = new DistroMetricsSettings(_mockAgent);
        Connect();

        var changedCount = 0;
        _settings.Changed += () => changedCount++;

        _settings.Disable();

        Mock.Arrange(() => _mockConfig.OtlpResourceAttributes).Returns(new Dictionary<string, string> { { "server.attr", "changed" } });
        EventBus<ServerConfigurationUpdatedEvent>.Publish(new ServerConfigurationUpdatedEvent(null));
        Connect(NewConnectionInfo(host: "another-host.example.com"));

        Assert.Multiple(() =>
        {
            Assert.That(changedCount, Is.EqualTo(0));
            Assert.That(_settings.IsDisabled, Is.True);
        });
    }

    [Test]
    public void Changed_HandlerThatThrows_IsCaughtAndLoggedOnce()
    {
        _settings = new DistroMetricsSettings(_mockAgent);
        _settings.Changed += () => throw new InvalidOperationException("boom");

        Assert.DoesNotThrow(() => Connect());

        // A second change (a different host) raises Changed again; the throwing handler fires again but must not
        // be logged a second time.
        Assert.DoesNotThrow(() => Connect(NewConnectionInfo(host: "second-host.example.com")));

        Assert.That(_warnings, Has.Count.EqualTo(1));
    }
}
