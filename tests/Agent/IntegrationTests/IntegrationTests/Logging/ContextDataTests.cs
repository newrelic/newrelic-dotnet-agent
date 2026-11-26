// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using Xunit;

namespace NewRelic.Agent.IntegrationTests.Logging.ContextData;

public abstract class ContextDataTestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
    where TFixture : ConsoleDynamicMethodFixture
{
    private readonly TFixture _fixture;
    private List<LoggingFramework> _loggingFrameworks;
    private readonly bool _testNestedContexts;
    private readonly bool _prefixContextData;

    private const string InfoMessage = "HelloWorld";
    private const string CollisionMessage = "CollisionMessage";

    // There are several entries in this dictionary to allow for different methods of adding the values in the test adapter

    private Dictionary<string, string> GetExpectedAttributes(LoggingFramework framework) => new Dictionary<string, string>()
    {
        { "framework", framework.ToString() },
        { "mycontext1", "foo" },
        { "mycontext2", "bar" },
        { "mycontext3", "test" },
        { "mycontext4", "value" },
    };

    private string FlattenExpectedAttributes(Dictionary<string, string> attributes) => string.Join(",", attributes.Select(x => x.Key + "=" + x.Value).ToArray());

    public ContextDataTestsBase(TFixture fixture, ITestOutputHelper output, bool testNestedContexts, params LoggingFramework[] loggingFrameworks)
        : this(fixture, output, testNestedContexts, true, loggingFrameworks)
    {
    }

    public ContextDataTestsBase(TFixture fixture, ITestOutputHelper output, bool testNestedContexts, bool prefixContextData, params LoggingFramework[] loggingFrameworks) : base(fixture)
    {
        _fixture = fixture;
        _loggingFrameworks = loggingFrameworks.ToList();
        _testNestedContexts = testNestedContexts;
        _prefixContextData = prefixContextData;
        _fixture.SetTimeout(TimeSpan.FromMinutes(2));
        _fixture.TestLogger = output;

        _loggingFrameworks.ForEach(x => _fixture.AddCommand($"LoggingTester SetFramework {x} {RandomPortGenerator.NextPort()}"));
        _fixture.AddCommand($"LoggingTester Configure");

        _loggingFrameworks.ForEach(x => _fixture.AddCommand($"LoggingTester CreateSingleLogMessageWithContext {x} {InfoMessage} {FlattenExpectedAttributes(GetExpectedAttributes(x))}"));

        if (!_prefixContextData) // a context key named like a standard log attribute must not replace the real value
            _loggingFrameworks.ForEach(x => _fixture.AddCommand($"LoggingTester CreateSingleLogMessageWithContext {x} {CollisionMessage} level=fromContext"));

        if (_testNestedContexts) // on supported frameworks, ensure that we don't blow up when accumulating the context key/value pairs
            _fixture.AddCommand($"LoggingTester LogMessageInNestedScopes");

        _fixture.AddActions
        (
            setupConfiguration: () =>
            {
                var configModifier = new NewRelicConfigModifier(fixture.DestinationNewRelicConfigFilePath);

                configModifier
                    .EnableContextData(true)
                    .SetLogLevel("debug");

                if (!_prefixContextData)
                    configModifier.EnableContextDataPrefix(false);
            },
            exerciseApplication: () =>
            {
                _fixture.AgentLog.WaitForLogLine(AgentLogBase.LogDataLogLineRegex, TimeSpan.FromSeconds(30));
            }
        );

        _fixture.Initialize();
    }

    [Fact]
    public void Test()
    {
        List<Assertions.ExpectedLogLine> expectedLogLines = new List<Assertions.ExpectedLogLine>();
        _loggingFrameworks.ForEach(x =>
        {
            var expectedAttributes = GetExpectedAttributes(x);

            // The log4net-based test adapters set only the first four context keys.
            if (x is LoggingFramework.Log4net or LoggingFramework.Sitecore)
                expectedAttributes.Remove("mycontext4");

            expectedLogLines.Add(
                new Assertions.ExpectedLogLine
                {
                    Level = LogUtils.GetLevelName(x, "INFO"),
                    LogMessage = InfoMessage,
                    Attributes = expectedAttributes
                });
        });

        var logLines = _fixture.AgentLog.GetLogEventDataLogLines().ToArray();

        // different versions of log4net add varying amounts of default attributes, we ignore the counts because of this
        var contextAttributePrefix = _prefixContextData ? "context." : string.Empty;
        Assertions.LogLinesExist(expectedLogLines, logLines, ignoreAttributeCount: true, contextAttributePrefix: contextAttributePrefix);

        if (!_prefixContextData)
        {
            var prefixedKeys = logLines
                .Where(l => l.Attributes != null)
                .SelectMany(l => l.Attributes.Keys)
                .Where(k => k.StartsWith("context.", StringComparison.Ordinal))
                .ToList();
            Assert.True(prefixedKeys.Count == 0, $"Found attribute keys with the context. prefix: {string.Join(", ", prefixedKeys)}");

            foreach (var framework in _loggingFrameworks)
            {
                var collisionLine = logLines.SingleOrDefault(l => l.Message == CollisionMessage && l.Level == LogUtils.GetLevelName(framework, "INFO"));
                Assert.True(collisionLine != null, $"No {CollisionMessage} log line with the real level was found for {framework}.");
                Assert.True(collisionLine.Attributes == null || !collisionLine.Attributes.ContainsKey("level"), "The context key `level` must be dropped, not sent as an attribute.");
            }
        }

        if (_testNestedContexts)
        {
            List<Assertions.ExpectedLogLine> contextExpectedLogLines = new List<Assertions.ExpectedLogLine>();
            _loggingFrameworks.ForEach(x =>
            {
                var outerContextExpectedAttributes = new Dictionary<string, string> { { "ScopeKey1", "scopeValue1" } };
                var innerContextExpectedAttributes = new Dictionary<string, string> { { "ScopeKey1", "scopeValue2" } };

                contextExpectedLogLines.Add(new Assertions.ExpectedLogLine
                {
                    Level = LogUtils.GetLevelName(x, "INFO"),
                    LogMessage = "Outer Scope",
                    Attributes = outerContextExpectedAttributes
                });
                contextExpectedLogLines.Add(new Assertions.ExpectedLogLine
                {
                    Level = LogUtils.GetLevelName(x, "INFO"),
                    LogMessage = "Inner Scope",
                    Attributes = innerContextExpectedAttributes
                });
            });

            Assertions.LogLinesExist(contextExpectedLogLines, logLines, ignoreAttributeCount: true, contextAttributePrefix: contextAttributePrefix);
        }
    }
}

#region log4net

[Trait("Platform", "WindowsOnly")]
public class Log4NetContextDataFWLatestTests : ContextDataTestsBase<ConsoleDynamicMethodFixtureFWLatest>
{
    public Log4NetContextDataFWLatestTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
        : base(fixture, output, false, LoggingFramework.Log4net)
    {
    }
}

[Trait("Platform", "WindowsOnly")]
public class Log4NetContextDataFW471Tests : ContextDataTestsBase<ConsoleDynamicMethodFixtureFW471>
{
    public Log4NetContextDataFW471Tests(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output)
        : base(fixture, output, false, LoggingFramework.Log4net)
    {
    }
}

[Trait("Platform", "WindowsOnly")]
public class Log4NetContextDataFW462Tests : ContextDataTestsBase<ConsoleDynamicMethodFixtureFW462>
{
    public Log4NetContextDataFW462Tests(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output)
        : base(fixture, output, false, LoggingFramework.Log4net)
    {
    }
}

public class Log4NetContextDataNetCoreLatestTests : ContextDataTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
{
    public Log4NetContextDataNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
        : base(fixture, output, false, LoggingFramework.Log4net)
    {
    }
}

public class Log4NetContextDataNetCoreOldestTests : ContextDataTestsBase<ConsoleDynamicMethodFixtureCoreOldest>
{
    public Log4NetContextDataNetCoreOldestTests(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output)
        : base(fixture, output, false, LoggingFramework.Log4net)
    {
    }
}

#endregion

#region NLog

[Trait("Platform", "WindowsOnly")]
public class NLogContextDataFWLatestTests : ContextDataTestsBase<ConsoleDynamicMethodFixtureFWLatest>
{
    public NLogContextDataFWLatestTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
        : base(fixture, output, false, LoggingFramework.NLog)
    {
    }
}

[Trait("Platform", "WindowsOnly")]
public class NLogContextDataFW471Tests : ContextDataTestsBase<ConsoleDynamicMethodFixtureFW471>
{
    public NLogContextDataFW471Tests(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output)
        : base(fixture, output, false, LoggingFramework.NLog)
    {
    }
}

[Trait("Platform", "WindowsOnly")]
public class NLogContextDataFW462Tests : ContextDataTestsBase<ConsoleDynamicMethodFixtureFW462>
{
    public NLogContextDataFW462Tests(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output)
        : base(fixture, output, false, LoggingFramework.NLog)
    {
    }
}

public class NLogContextDataNetCoreLatestTests : ContextDataTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
{
    public NLogContextDataNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
        : base(fixture, output, false, LoggingFramework.NLog)
    {
    }
}

public class NLogContextDataNetCoreOldestTests : ContextDataTestsBase<ConsoleDynamicMethodFixtureCoreOldest>
{
    public NLogContextDataNetCoreOldestTests(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output)
        : base(fixture, output, false, LoggingFramework.NLog)
    {
    }
}

#endregion

#region Serilog

[Trait("Platform", "WindowsOnly")]
public class SerilogContextDataFWLatestTests : ContextDataTestsBase<ConsoleDynamicMethodFixtureFWLatest>
{
    public SerilogContextDataFWLatestTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
        : base(fixture, output, false, LoggingFramework.Serilog)
    {
    }
}

[Trait("Platform", "WindowsOnly")]
public class SerilogContextDataFW471Tests : ContextDataTestsBase<ConsoleDynamicMethodFixtureFW471>
{
    public SerilogContextDataFW471Tests(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output)
        : base(fixture, output, false, LoggingFramework.Serilog)
    {
    }
}

[Trait("Platform", "WindowsOnly")]
public class SerilogContextDataFW462Tests : ContextDataTestsBase<ConsoleDynamicMethodFixtureFW462>
{
    public SerilogContextDataFW462Tests(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output)
        : base(fixture, output, false, LoggingFramework.Serilog)
    {
    }
}

public class SerilogContextDataNetCoreLatestTests : ContextDataTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
{
    public SerilogContextDataNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
        : base(fixture, output, false, LoggingFramework.Serilog)
    {
    }
}

public class SerilogContextDataPrefixOffNetCoreLatestTests : ContextDataTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
{
    public SerilogContextDataPrefixOffNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
        : base(fixture, output, false, false, LoggingFramework.Serilog)
    {
    }
}

public class SerilogContextDataNetCoreOldestTests : ContextDataTestsBase<ConsoleDynamicMethodFixtureCoreOldest>
{
    public SerilogContextDataNetCoreOldestTests(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output)
        : base(fixture, output, false, LoggingFramework.Serilog)
    {
    }
}

#endregion

#region MEL

[Trait("Platform", "WindowsOnly")]
public class MELContextDataFWLatestTests : ContextDataTestsBase<ConsoleDynamicMethodFixtureFWLatest>
{
    public MELContextDataFWLatestTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
        : base(fixture, output, true, LoggingFramework.MicrosoftLogging)
    {
    }
}

public class MELContextDataNetCoreLatestTests : ContextDataTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
{
    public MELContextDataNetCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
        : base(fixture, output, true, LoggingFramework.MicrosoftLogging)
    {
    }
}

public class MELContextDataNetCoreOldestTests : ContextDataTestsBase<ConsoleDynamicMethodFixtureCoreOldest>
{
    public MELContextDataNetCoreOldestTests(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output)
        : base(fixture, output, true, LoggingFramework.MicrosoftLogging)
    {
    }
}

#endregion

#region Sitecore
// Disabled: Dependabot's proxy blocks the Sitecore NuGet feed. To re-enable, define SITECORE_LOGGING
// and restore the Sitecore.Logging references in MultiFunctionApplicationHelpers.csproj.
#if SITECORE_LOGGING
[Trait("Platform", "WindowsOnly")]
public class SitecoreContextDataFWLatestTests : ContextDataTestsBase<ConsoleDynamicMethodFixtureFWLatest>
{
    public SitecoreContextDataFWLatestTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
        : base(fixture, output, false, LoggingFramework.Sitecore)
    {
    }
}

[Trait("Platform", "WindowsOnly")]
public class SitecoreContextDataFW48Tests : ContextDataTestsBase<ConsoleDynamicMethodFixtureFW48>
{
    public SitecoreContextDataFW48Tests(ConsoleDynamicMethodFixtureFW48 fixture, ITestOutputHelper output)
        : base(fixture, output, false, LoggingFramework.Sitecore)
    {
    }
}

[Trait("Platform", "WindowsOnly")]
public class SitecorePlusLog4NetContextDataFWLatestTests : ContextDataTestsBase<ConsoleDynamicMethodFixtureFWLatest>
{
    public SitecorePlusLog4NetContextDataFWLatestTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
        : base(fixture, output, false, LoggingFramework.Sitecore, LoggingFramework.Log4net)
    {
    }
}

[Trait("Platform", "WindowsOnly")]
public class SitecorePlusLog4NetContextDataFW48Tests : ContextDataTestsBase<ConsoleDynamicMethodFixtureFW48>
{
    public SitecorePlusLog4NetContextDataFW48Tests(ConsoleDynamicMethodFixtureFW48 fixture, ITestOutputHelper output)
        : base(fixture, output, false, LoggingFramework.Sitecore, LoggingFramework.Log4net)
    {
    }
}

#endif
#endregion // Sitecore

#region SEL
[Trait("Platform", "WindowsOnly")]
public class SELContextDataFWLatestTests : ContextDataTestsBase<ConsoleDynamicMethodFixtureFWLatest>
{
    public SELContextDataFWLatestTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
        : base(fixture, output, true, LoggingFramework.SerilogEL)
    {
    }
}

[Trait("Platform", "WindowsOnly")]
public class SELContextDataFW48Tests : ContextDataTestsBase<ConsoleDynamicMethodFixtureFW48>
{
    public SELContextDataFW48Tests(ConsoleDynamicMethodFixtureFW48 fixture, ITestOutputHelper output)
        : base(fixture, output, true, LoggingFramework.SerilogEL)
    {
    }
}

public class SELContextDataCoreLatestTests : ContextDataTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
{
    public SELContextDataCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
        : base(fixture, output, true, LoggingFramework.SerilogEL)
    {
    }
}

public class SELContextDataCoreOldestTests : ContextDataTestsBase<ConsoleDynamicMethodFixtureCoreOldest>
{
    public SELContextDataCoreOldestTests(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output)
        : base(fixture, output, true, LoggingFramework.SerilogEL)
    {
    }
}

#endregion // SEL

#region NEL
[Trait("Platform", "WindowsOnly")]
public class NELContextDataFWLatestTests : ContextDataTestsBase<ConsoleDynamicMethodFixtureFWLatest>
{
    public NELContextDataFWLatestTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
        : base(fixture, output, true, LoggingFramework.NLogEL)
    {
    }
}

public class NELContextDataCoreLatestTests : ContextDataTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
{
    public NELContextDataCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
        : base(fixture, output, true, LoggingFramework.NLogEL)
    {
    }
}

#endregion // NEL
