// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using NewRelic.Agent.Core.Commands;
using NewRelic.Agent.Core.ContinuousProfiling;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.UnitTest.Commands;

[TestFixture]
public class StartContinuousProfilerCommandTests
{
    private IContinuousProfilingCommandTarget _target;
    private StartContinuousProfilerCommand _command;

    [SetUp]
    public void SetUp()
    {
        _target = Mock.Create<IContinuousProfilingCommandTarget>();
        _command = new StartContinuousProfilerCommand(_target, new AckOnlyContinuousProfilerResponseFormatter());
    }

    [Test]
    public void Name_is_start_continuous_profiler()
    {
        Assert.That(_command.Name, Is.EqualTo("start_continuous_profiler"));
    }

    [Test]
    public void Process_parses_arguments_and_delegates_to_the_target()
    {
        var expectedResult = new ContinuousProfilingCommandResult(new[] { "cpu" }, 10000, 10000, new Dictionary<string, string>());
        Mock.Arrange(() => _target.StartFromCommand(Arg.IsAny<IReadOnlyList<string>>(), Arg.IsAny<int?>(), Arg.IsAny<int?>()))
            .Returns(expectedResult);

        var arguments = new Dictionary<string, object> { { "include", new JArray("cpu") } };

        var response = (IDictionary<string, object>)_command.Process(arguments);

        Mock.Assert(() => _target.StartFromCommand(Arg.Matches<IReadOnlyList<string>>(l => l.Count == 1 && l[0] == "cpu"), null, null), Occurs.Once());
        Assert.That(response, Is.Empty); // ack-only formatter
    }
}
