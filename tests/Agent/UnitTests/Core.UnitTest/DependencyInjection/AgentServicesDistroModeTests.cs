// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using Autofac.Core.Registration;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Fixtures;
using NewRelic.Agent.Core.OpenTelemetryBridge.Metrics;
using NewRelic.Agent.Core.OpenTelemetryBridge.Metrics.Interfaces;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.DependencyInjection;

[TestFixture]
public class AgentServicesDistroModeTests
{
    private static IConfigurationService CreateConfigurationService(out IConfiguration configuration)
    {
        // Prevent the ConnectionManager from trying to connect to anything
        configuration = Mock.Create<IConfiguration>();
        var localConfiguration = configuration;
        Mock.Arrange(() => localConfiguration.AutoStartAgent).Returns(false);
        Mock.Arrange(() => localConfiguration.NewRelicConfigFilePath).Returns("c:\\");
        var configurationService = Mock.Create<IConfigurationService>();
        Mock.Arrange(() => configurationService.Configuration).Returns(localConfiguration);
        return configurationService;
    }

    [Test]
    public void RegisterServices_DistroMode_RegistersNoOpNativeMethods()
    {
        CreateConfigurationService(out var configuration);

        using (new ConfigurationAutoResponder(configuration))
        using (var container = AgentServices.GetContainer())
        {
            AgentServices.RegisterServices(container, false, false, true);

            Assert.That(container.Resolve<INativeMethods>(), Is.InstanceOf<NoOpNativeMethods>());
        }
    }

    [Test]
    public void RegisterServices_DistroMode_DoesNotRegisterMeterListenerBridge()
    {
        CreateConfigurationService(out var configuration);

        using (new ConfigurationAutoResponder(configuration))
        using (var container = AgentServices.GetContainer())
        {
            AgentServices.RegisterServices(container, false, false, true);

            Assert.Throws<ComponentNotRegisteredException>(() => container.Resolve<MeterListenerBridge>());
            Assert.Throws<ComponentNotRegisteredException>(() => container.Resolve<IOtlpExporterConfigurationService>());
        }
    }

    [Test]
    public void RegisterServices_DistroAndServerlessMode_TreatedAsDistro()
    {
        CreateConfigurationService(out var configuration);

        using (new ConfigurationAutoResponder(configuration))
        using (var container = AgentServices.GetContainer())
        {
            AgentServices.RegisterServices(container, true, false, true);

            Assert.That(container.Resolve<INativeMethods>(), Is.InstanceOf<NoOpNativeMethods>());
            Assert.Throws<ComponentNotRegisteredException>(() => container.Resolve<MeterListenerBridge>());
        }
    }

    [Test]
    public void RegisterServices_DefaultMode_RegistersPlatformNativeMethods()
    {
        CreateConfigurationService(out var configuration);

        using (new ConfigurationAutoResponder(configuration))
        using (var container = AgentServices.GetContainer())
        {
            AgentServices.RegisterServices(container, false, false, false);

            var nativeMethods = container.Resolve<INativeMethods>();
            if (AgentInstallConfiguration.IsWindows)
                Assert.That(nativeMethods, Is.InstanceOf<WindowsNativeMethods>());
            else
                Assert.That(nativeMethods, Is.InstanceOf<LinuxNativeMethods>());
            Assert.That(container.Resolve<MeterListenerBridge>(), Is.Not.Null);
        }
    }

    [Test]
    public void RegisterServices_ThreeArgOverload_MatchesDistroFalse()
    {
        CreateConfigurationService(out var configuration);

        using (new ConfigurationAutoResponder(configuration))
        using (var threeArgContainer = AgentServices.GetContainer())
        using (var fourArgContainer = AgentServices.GetContainer())
        {
            AgentServices.RegisterServices(threeArgContainer, false, false);
            AgentServices.RegisterServices(fourArgContainer, false, false, false);

            Assert.That(threeArgContainer.Resolve<INativeMethods>().GetType(), Is.EqualTo(fourArgContainer.Resolve<INativeMethods>().GetType()));
            Assert.That(threeArgContainer.Resolve<MeterListenerBridge>(), Is.Not.Null);
            Assert.That(fourArgContainer.Resolve<MeterListenerBridge>(), Is.Not.Null);
        }
    }

    [Test]
    public void StartServices_DistroMode_DoesNotResolveMeterListenerBridge()
    {
        var configurationService = CreateConfigurationService(out var configuration);

        using (new ConfigurationAutoResponder(configuration))
        using (var container = AgentServices.GetContainer())
        {
            AgentServices.RegisterServices(container, false, false, true);

            container.ReplaceInstanceRegistration(configurationService);
#if NET
            container.ReplaceRegistrations(); // creates a new scope, registering the replacement instances from all .ReplaceRegistration() calls above
#endif

            // MeterListenerBridge is not registered in distro mode, so resolving it would throw
            Assert.DoesNotThrow(() => AgentServices.StartServices(container, false, false, true));
        }
    }

    [Test]
    public void StartServices_ThreeArgOverload_ForwardsWithDistroFalse()
    {
        var configurationService = CreateConfigurationService(out var configuration);

        using (new ConfigurationAutoResponder(configuration))
        using (var container = AgentServices.GetContainer())
        {
            AgentServices.RegisterServices(container, false, false);

            container.ReplaceInstanceRegistration(configurationService);
#if NET
            container.ReplaceRegistrations();
#endif

            Assert.DoesNotThrow(() => AgentServices.StartServices(container, false, false));
        }
    }

    [Test]
    public void NoOpNativeMethods_AllMembers_DoNothing()
    {
        var nativeMethods = new NoOpNativeMethods();

        Assert.DoesNotThrow(() => nativeMethods.ReleaseProfile());
        Assert.DoesNotThrow(() => nativeMethods.ShutdownNativeThreadProfiler());

        Assert.That(nativeMethods.RequestFunctionNames(new[] { UIntPtr.Zero }, 1, out var functionInfo), Is.EqualTo(0));
        Assert.That(functionInfo, Is.EqualTo(IntPtr.Zero));
        Assert.That(nativeMethods.RequestProfile(out var snapshots, out var length), Is.EqualTo(0));
        Assert.That(snapshots, Is.EqualTo(IntPtr.Zero));
        Assert.That(length, Is.EqualTo(0));

        Assert.That(nativeMethods.InstrumentationRefresh(), Is.EqualTo(0));
        Assert.That(nativeMethods.ReloadConfiguration(), Is.EqualTo(0));
        Assert.That(nativeMethods.AddCustomInstrumentation("name", "<xml/>"), Is.EqualTo(0));
        Assert.That(nativeMethods.ApplyCustomInstrumentation(), Is.EqualTo(0));

        Assert.That(nativeMethods.ContinuousProfilerStart(1000), Is.EqualTo(0));
        Assert.That(nativeMethods.ContinuousProfilerStop(), Is.EqualTo(0));
        Assert.That(nativeMethods.ContinuousProfilerReadThreadSamples(4, new byte[4]), Is.EqualTo(0));
        Assert.That(nativeMethods.ContinuousProfilerSetTraceContext(1, 2, 3), Is.EqualTo(0));
        Assert.That(nativeMethods.ContinuousProfilerResetTraceContext(), Is.EqualTo(0));
        Assert.That(nativeMethods.ContinuousProfilerRetireSpans(new long[] { 1 }, 1), Is.EqualTo(0));
        Assert.That(nativeMethods.ContinuousProfilerGetPendingPushCell(out var cell), Is.EqualTo(0));
        Assert.That(cell, Is.EqualTo(IntPtr.Zero));
        Assert.That(nativeMethods.ContinuousProfilerSetAgentWork(), Is.EqualTo(0));
        Assert.That(nativeMethods.ContinuousProfilerResetAgentWork(), Is.EqualTo(0));
        Assert.That(nativeMethods.ContinuousProfilerShutdown(), Is.EqualTo(0));
    }
}
