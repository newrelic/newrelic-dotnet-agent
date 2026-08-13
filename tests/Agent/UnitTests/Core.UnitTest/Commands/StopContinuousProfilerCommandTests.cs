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
public class StopContinuousProfilerCommandTests
{
    private IContinuousProfilingCommandTarget _target;
    private StopContinuousProfilerCommand _command;

    [SetUp]
    public void SetUp()
    {
        _target = Mock.Create<IContinuousProfilingCommandTarget>();
        _command = new StopContinuousProfilerCommand(_target, new AckOnlyContinuousProfilerResponseFormatter());
    }

    [Test]
    public void Name_is_stop_continuous_profiler()
    {
        Assert.That(_command.Name, Is.EqualTo("stop_continuous_profiler"));
    }

    [Test]
    public void Process_parses_arguments_and_delegates_to_the_target()
    {
        var expectedResult = new ContinuousProfilingCommandResult(System.Array.Empty<string>(), 10000, 10000, new Dictionary<string, string>());
        Mock.Arrange(() => _target.StopFromCommand(Arg.IsAny<IReadOnlyList<string>>())).Returns(expectedResult);

        var arguments = new Dictionary<string, object> { { "include", new JArray("cpu") } };

        var response = (IDictionary<string, object>)_command.Process(arguments);

        Mock.Assert(() => _target.StopFromCommand(Arg.Matches<IReadOnlyList<string>>(l => l.Count == 1 && l[0] == "cpu")), Occurs.Once());
        Assert.That(response, Is.Empty);
    }
}
