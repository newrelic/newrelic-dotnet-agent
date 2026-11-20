// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using NewRelic.Agent.Extensions.Logging;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.OtelDistro;

[TestFixture]
public class DistroRegistrationRouterTests
{
    private const string Location = "default AppDomain, in-process";
    private const string InUse = "OTel distro registration coordinator in use (default AppDomain, in-process).";
    private const string Unavailable = "OTel distro registration coordinator unavailable (no coordinator here); this AppDomain registers with OTel on its own.";

    private static readonly IReadOnlyList<int> Sites = new[] { 1, 2, 3 };

    private DistroRecordingLogger _logger;
    private IDistroSiteRegistrar _registrar;
    private int _fallbackCalls;
    private bool _fallbackResult;
    private string _fallbackFailure;

    [SetUp]
    public void SetUp()
    {
        _logger = new DistroRecordingLogger();
        Log.Initialize(_logger);
        _registrar = Mock.Create<IDistroSiteRegistrar>();
        _fallbackCalls = 0;
        _fallbackResult = true;
        _fallbackFailure = null;
    }

    [TearDown]
    public void TearDown()
    {
        Log.Initialize(new NoOpLogger());
    }

    private bool Fallback(IReadOnlyList<int> sites, out string failure)
    {
        _fallbackCalls++;
        failure = _fallbackFailure;
        return _fallbackResult;
    }

    private DistroRegistrationRouter Router(IDistroSiteRegistrar coordinator) =>
        new DistroRegistrationRouter(coordinator, Location, "no coordinator here", Fallback);

    private void Returns(DistroSiteRegistration registration) =>
        Mock.Arrange(() => _registrar.Register(Arg.IsAny<IReadOnlyList<int>>())).Returns(registration);

    private static DistroSiteRegistration Result(DistroSiteRegistrationOutcome outcome, int requested = 3, int sent = 0, string failure = null) =>
        new DistroSiteRegistration(outcome, requested, sent, failure);

    private string[] Infos() => _logger.Messages(Level.Info);

    // Start-up

    [Test]
    public void Startup_Sent_ReturnsTrue_LogsInUseAndCounts()
    {
        Returns(Result(DistroSiteRegistrationOutcome.Sent, requested: 3, sent: 2));
        var router = Router(_registrar);

        var handled = router.RegisterAtStartup(Sites);

        Assert.Multiple(() =>
        {
            Assert.That(handled, Is.True);
            Assert.That(Infos(), Is.EqualTo(new[]
            {
                InUse,
                "NR instrumentation registered through the coordinator at start-up: 3 requested, 2 sent, 1 already registered in this process.",
            }));
            Assert.That(_fallbackCalls, Is.EqualTo(0));
        });
    }

    [Test]
    public void Startup_AllPresent_ReturnsTrue()
    {
        Returns(Result(DistroSiteRegistrationOutcome.AllPresent, requested: 3, sent: 0));

        var handled = Router(_registrar).RegisterAtStartup(Sites);

        Assert.Multiple(() =>
        {
            Assert.That(handled, Is.True);
            Assert.That(Infos(), Is.EqualTo(new[]
            {
                InUse,
                "NR instrumentation registered through the coordinator at start-up: 3 requested, 0 sent, 3 already registered in this process.",
            }));
        });
    }

    [Test]
    public void Startup_InUseLoggedOnlyOnce()
    {
        Returns(Result(DistroSiteRegistrationOutcome.Sent, requested: 3, sent: 3));
        var router = Router(_registrar);

        router.RegisterAtStartup(Sites);
        router.RegisterAtStartup(Sites);

        Assert.That(Array.FindAll(Infos(), m => m == InUse), Has.Length.EqualTo(1));
    }

    [Test]
    public void Startup_Failed_ReturnsFalse_LogsFallbackReason()
    {
        Returns(Result(DistroSiteRegistrationOutcome.Failed, failure: "OTel returned -1"));

        var handled = Router(_registrar).RegisterAtStartup(Sites);

        Assert.Multiple(() =>
        {
            Assert.That(handled, Is.False);
            Assert.That(Infos(), Is.EqualTo(new[]
            {
                InUse,
                "NR instrumentation could not be registered through the coordinator at start-up (OTel returned -1); using OTel's start-up payload.",
            }));
            Assert.That(_fallbackCalls, Is.EqualTo(0), "start-up fallback is OTel's own payload, not the run-time delegate");
        });
    }

    [Test]
    public void Startup_FailedWithoutReason_NamesTheOutcome()
    {
        Returns(Result(DistroSiteRegistrationOutcome.Failed));

        var handled = Router(_registrar).RegisterAtStartup(Sites);

        Assert.Multiple(() =>
        {
            Assert.That(handled, Is.False);
            Assert.That(Infos()[1], Is.EqualTo("NR instrumentation could not be registered through the coordinator at start-up (Failed); using OTel's start-up payload."));
        });
    }

    [Test]
    public void Startup_Unavailable_ReturnsFalse()
    {
        Returns(Result(DistroSiteRegistrationOutcome.Unavailable, failure: "coordinator disconnected"));

        var handled = Router(_registrar).RegisterAtStartup(Sites);

        Assert.Multiple(() =>
        {
            Assert.That(handled, Is.False);
            Assert.That(Infos()[1], Is.EqualTo("NR instrumentation could not be registered through the coordinator at start-up (coordinator disconnected); using OTel's start-up payload."));
            Assert.That(_fallbackCalls, Is.EqualTo(0));
        });
    }

    [Test]
    public void Startup_RegistrarThrows_ReturnsFalse()
    {
        Mock.Arrange(() => _registrar.Register(Arg.IsAny<IReadOnlyList<int>>())).Throws(new InvalidOperationException("boom"));

        var router = Router(_registrar);
        bool handled = true;

        Assert.DoesNotThrow(() => handled = router.RegisterAtStartup(Sites));

        Assert.Multiple(() =>
        {
            Assert.That(handled, Is.False);
            Assert.That(Infos()[1], Is.EqualTo("NR instrumentation could not be registered through the coordinator at start-up (InvalidOperationException: boom); using OTel's start-up payload."));
        });
    }

    [Test]
    public void Startup_RegistrarReturnsNull_ReturnsFalse()
    {
        Returns(null);

        var handled = Router(_registrar).RegisterAtStartup(Sites);

        Assert.Multiple(() =>
        {
            Assert.That(handled, Is.False);
            Assert.That(Infos()[1], Does.Contain("the registration coordinator returned no result"));
        });
    }

    [Test]
    public void Startup_Failed_LatchesRunTimeFallback()
    {
        Returns(Result(DistroSiteRegistrationOutcome.Failed, failure: "OTel returned -1"));
        var router = Router(_registrar);
        router.RegisterAtStartup(Sites);
        // The coordinator would now succeed, but start-up already used OTel's payload, so live edits take the fallback.
        Returns(Result(DistroSiteRegistrationOutcome.Sent, sent: 3));

        var handled = router.RegisterAtRunTime(Sites, out var failure);

        Assert.Multiple(() =>
        {
            Assert.That(handled, Is.True);
            Assert.That(failure, Is.Null);
            Assert.That(_fallbackCalls, Is.EqualTo(1));
            Assert.That(Infos().Length, Is.EqualTo(2), "in-use and the start-up fallback line only; no new line");
            Mock.Assert(() => _registrar.Register(Arg.IsAny<IReadOnlyList<int>>()), Occurs.Once());
        });
    }

    [Test]
    public void Startup_Unavailable_LatchesRunTimeFallback()
    {
        Returns(Result(DistroSiteRegistrationOutcome.Unavailable, failure: "coordinator disconnected"));
        var router = Router(_registrar);
        router.RegisterAtStartup(Sites);

        var handled = router.RegisterAtRunTime(Sites, out _);

        Assert.Multiple(() =>
        {
            Assert.That(handled, Is.True);
            Assert.That(_fallbackCalls, Is.EqualTo(1));
            Assert.That(Infos().Length, Is.EqualTo(2));
            Mock.Assert(() => _registrar.Register(Arg.IsAny<IReadOnlyList<int>>()), Occurs.Once());
        });
    }

    [Test]
    public void Startup_Throws_LatchesRunTimeFallback()
    {
        Mock.Arrange(() => _registrar.Register(Arg.IsAny<IReadOnlyList<int>>())).Throws(new InvalidOperationException("boom"));
        var router = Router(_registrar);
        router.RegisterAtStartup(Sites);

        var handled = router.RegisterAtRunTime(Sites, out _);

        Assert.Multiple(() =>
        {
            Assert.That(handled, Is.True);
            Assert.That(_fallbackCalls, Is.EqualTo(1));
            Assert.That(Infos().Length, Is.EqualTo(2));
            Mock.Assert(() => _registrar.Register(Arg.IsAny<IReadOnlyList<int>>()), Occurs.Once());
        });
    }

    [Test]
    public void Startup_Sent_DoesNotLatch()
    {
        Returns(Result(DistroSiteRegistrationOutcome.Sent, sent: 3));
        var router = Router(_registrar);
        router.RegisterAtStartup(Sites);

        var handled = router.RegisterAtRunTime(Sites, out _);

        Assert.Multiple(() =>
        {
            Assert.That(handled, Is.True);
            Assert.That(_fallbackCalls, Is.EqualTo(0));
            Mock.Assert(() => _registrar.Register(Arg.IsAny<IReadOnlyList<int>>()), Occurs.Exactly(2));
        });
    }

    [Test]
    public void NullCoordinator_Startup_ReturnsFalse_LogsUnavailableOnce()
    {
        var router = Router(null);

        var first = router.RegisterAtStartup(Sites);
        var second = router.RegisterAtStartup(Sites);

        Assert.Multiple(() =>
        {
            Assert.That(first, Is.False);
            Assert.That(second, Is.False);
            Assert.That(Infos(), Is.EqualTo(new[] { Unavailable }));
            Assert.That(_fallbackCalls, Is.EqualTo(0), "start-up fallback is OTel's own payload, which the plugin handles");
        });
    }

    // Run time

    [Test]
    public void NullCoordinator_RunTime_CallsFallbackAndReturnsItsResultAndFailure()
    {
        _fallbackResult = false;
        _fallbackFailure = "OTel 1.18.0.0: EntryPointNotFoundException";
        var router = Router(null);

        var ok = router.RegisterAtRunTime(Sites, out var failure);
        router.RegisterAtRunTime(Sites, out _);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.False);
            Assert.That(failure, Is.EqualTo("OTel 1.18.0.0: EntryPointNotFoundException"));
            Assert.That(_fallbackCalls, Is.EqualTo(2));
            Assert.That(Infos(), Is.EqualTo(new[] { Unavailable }));
        });
    }

    [Test]
    public void NullCoordinator_RunTime_FallbackSucceeds_ReturnsTrue()
    {
        var ok = Router(null).RegisterAtRunTime(Sites, out var failure);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(failure, Is.Null);
        });
    }

    [Test]
    public void NullCoordinator_StartupThenRunTime_LogsUnavailableOnce()
    {
        var router = Router(null);

        router.RegisterAtStartup(Sites);
        router.RegisterAtRunTime(Sites, out _);

        Assert.That(Infos(), Is.EqualTo(new[] { Unavailable }));
    }

    [Test]
    public void RunTime_Sent_And_AllPresent_ReturnTrue_NullFailure()
    {
        var router = Router(_registrar);

        Returns(Result(DistroSiteRegistrationOutcome.Sent, requested: 3, sent: 3));
        var sentOk = router.RegisterAtRunTime(Sites, out var sentFailure);
        var afterSent = Infos();

        Returns(Result(DistroSiteRegistrationOutcome.AllPresent, requested: 3));
        var presentOk = router.RegisterAtRunTime(Sites, out var presentFailure);

        Assert.Multiple(() =>
        {
            Assert.That(sentOk, Is.True);
            Assert.That(sentFailure, Is.Null);
            Assert.That(afterSent, Is.Empty, "a run-time Sent logs nothing here; the caller reports it");
            Assert.That(presentOk, Is.True);
            Assert.That(presentFailure, Is.Null);
            Assert.That(Infos(), Is.EqualTo(new[] { "NR instrumentation for a live XML change was already registered in this process (3 requested); no OTel call." }));
            Assert.That(_fallbackCalls, Is.EqualTo(0));
        });
    }

    [Test]
    public void RunTime_Failed_ReturnsFalse_WithFailure_NoFallbackCall()
    {
        Returns(Result(DistroSiteRegistrationOutcome.Failed, failure: "OTel returned -1"));

        var ok = Router(_registrar).RegisterAtRunTime(Sites, out var failure);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.False);
            Assert.That(failure, Is.EqualTo("OTel returned -1"));
            Assert.That(_fallbackCalls, Is.EqualTo(0));
            Assert.That(Infos(), Is.Empty);
        });
    }

    [Test]
    public void RunTime_FailedWithoutReason_StillGivesAFailure()
    {
        Returns(Result(DistroSiteRegistrationOutcome.Failed));

        var ok = Router(_registrar).RegisterAtRunTime(Sites, out var failure);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.False);
            Assert.That(failure, Is.Not.Empty);
        });
    }

    [Test]
    public void RunTime_Unavailable_CallsFallback_ThenAlwaysFallback_LogOnce()
    {
        Returns(Result(DistroSiteRegistrationOutcome.Unavailable, failure: "coordinator disconnected"));
        _fallbackFailure = "fallback said no";
        _fallbackResult = false;
        var router = Router(_registrar);

        var first = router.RegisterAtRunTime(Sites, out var firstFailure);
        // The coordinator would now succeed, but the router has given up on it.
        Returns(Result(DistroSiteRegistrationOutcome.Sent, sent: 3));
        var second = router.RegisterAtRunTime(Sites, out var secondFailure);

        Assert.Multiple(() =>
        {
            Assert.That(first, Is.False);
            Assert.That(firstFailure, Is.EqualTo("fallback said no"));
            Assert.That(second, Is.False);
            Assert.That(secondFailure, Is.EqualTo("fallback said no"));
            Assert.That(_fallbackCalls, Is.EqualTo(2));
            Assert.That(Infos(), Is.EqualTo(new[] { "NR instrumentation could not be registered through the coordinator (coordinator disconnected); this AppDomain registers with OTel on its own from now on." }));
            Mock.Assert(() => _registrar.Register(Arg.IsAny<IReadOnlyList<int>>()), Occurs.Once());
        });
    }

    [Test]
    public void RunTime_UnavailableWithoutReason_NamesTheOutcome()
    {
        Returns(Result(DistroSiteRegistrationOutcome.Unavailable));

        Router(_registrar).RegisterAtRunTime(Sites, out _);

        Assert.That(Infos(), Is.EqualTo(new[] { "NR instrumentation could not be registered through the coordinator (Unavailable); this AppDomain registers with OTel on its own from now on." }));
    }

    [Test]
    public void RunTime_RegistrarThrows_TreatedAsUnavailable()
    {
        Mock.Arrange(() => _registrar.Register(Arg.IsAny<IReadOnlyList<int>>())).Throws(new InvalidOperationException("boom"));
        var router = Router(_registrar);
        bool ok = false;
        string failure = null;

        Assert.DoesNotThrow(() => ok = router.RegisterAtRunTime(Sites, out failure));

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True, "the fallback's result");
            Assert.That(failure, Is.Null);
            Assert.That(_fallbackCalls, Is.EqualTo(1));
            Assert.That(Infos(), Is.EqualTo(new[] { "NR instrumentation could not be registered through the coordinator (InvalidOperationException: boom); this AppDomain registers with OTel on its own from now on." }));
        });
    }

    [Test]
    public void RunTime_UnavailableObservedAfterAnotherCallAlreadySwitched_LogsOnce()
    {
        // The registrar re-enters the router once, standing in for two threads that both see Unavailable.
        DistroRegistrationRouter router = null;
        var depth = 0;
        var registrar = new LambdaRegistrar(requested =>
        {
            if (depth++ == 0)
            {
                router.RegisterAtRunTime(Sites, out _);
            }

            return Result(DistroSiteRegistrationOutcome.Unavailable, failure: "gone");
        });
        router = Router(registrar);

        router.RegisterAtRunTime(Sites, out _);

        Assert.Multiple(() =>
        {
            Assert.That(Infos(), Has.Length.EqualTo(1));
            Assert.That(_fallbackCalls, Is.EqualTo(2));
        });
    }

    [Test]
    public void RunTime_FallbackThrows_ReturnsFalseWithTheException()
    {
        var router = new DistroRegistrationRouter(null, Location, "no coordinator here", (IReadOnlyList<int> sites, out string failure) => throw new InvalidOperationException("fallback boom"));
        bool ok = true;
        string failure = null;

        Assert.DoesNotThrow(() => ok = router.RegisterAtRunTime(Sites, out failure));

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.False);
            Assert.That(failure, Is.EqualTo("InvalidOperationException: fallback boom"));
        });
    }

    [Test]
    public void LoggerThrows_NothingEscapes()
    {
        Log.Initialize(new ThrowingLogger());
        Returns(Result(DistroSiteRegistrationOutcome.Unavailable, failure: "gone"));
        var router = Router(_registrar);

        Assert.DoesNotThrow(() =>
        {
            router.RegisterAtStartup(Sites);
            router.RegisterAtRunTime(Sites, out _);
            Router(null).RegisterAtStartup(Sites);
            Returns(Result(DistroSiteRegistrationOutcome.AllPresent));
            Router(_registrar).RegisterAtStartup(Sites);
            Router(_registrar).RegisterAtRunTime(Sites, out _);
        });
    }

    [Test]
    public void Ctor_NullFallback_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new DistroRegistrationRouter(_registrar, Location, null, null));
    }

    [Test]
    public void DistroSiteRegistration_ExposesItsValues()
    {
        var registration = new DistroSiteRegistration(DistroSiteRegistrationOutcome.Failed, 5, 2, "why");

        Assert.Multiple(() =>
        {
            Assert.That(registration.Outcome, Is.EqualTo(DistroSiteRegistrationOutcome.Failed));
            Assert.That(registration.Requested, Is.EqualTo(5));
            Assert.That(registration.Sent, Is.EqualTo(2));
            Assert.That(registration.Failure, Is.EqualTo("why"));
        });
    }

    private sealed class LambdaRegistrar : IDistroSiteRegistrar
    {
        private readonly Func<IReadOnlyList<int>, DistroSiteRegistration> _register;

        public LambdaRegistrar(Func<IReadOnlyList<int>, DistroSiteRegistration> register) => _register = register;

        public DistroSiteRegistration Register(IReadOnlyList<int> sites) => _register(sites);
    }

    private sealed class ThrowingLogger : ILogger
    {
        public bool IsDebugEnabled => true;
        public bool IsErrorEnabled => true;
        public bool IsFinestEnabled => true;
        public bool IsInfoEnabled => true;
        public bool IsWarnEnabled => true;

        public void Debug(Exception exception, string message, params object[] args) => throw new InvalidOperationException("logger");
        public void Debug(string message, params object[] args) => throw new InvalidOperationException("logger");
        public void Error(Exception exception, string message, params object[] args) => throw new InvalidOperationException("logger");
        public void Error(string message, params object[] args) => throw new InvalidOperationException("logger");
        public void Finest(Exception exception, string message, params object[] args) => throw new InvalidOperationException("logger");
        public void Finest(string message, params object[] args) => throw new InvalidOperationException("logger");
        public void Info(Exception exception, string message, params object[] args) => throw new InvalidOperationException("logger");
        public void Info(string message, params object[] args) => throw new InvalidOperationException("logger");
        public void Warn(Exception exception, string message, params object[] args) => throw new InvalidOperationException("logger");
        public void Warn(string message, params object[] args) => throw new InvalidOperationException("logger");
        public bool IsEnabledFor(Level level) => true;
        public void Log(Level level, string message) => throw new InvalidOperationException("logger");
        public void Log(Level level, Exception ex, string message) => throw new InvalidOperationException("logger");
    }
}
