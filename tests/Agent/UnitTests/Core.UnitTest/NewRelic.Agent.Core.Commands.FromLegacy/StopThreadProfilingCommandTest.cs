// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using NewRelic.Agent.Core.ThreadProfiling;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.Commands;

[TestFixture]
public class StopThreadProfilingCommandTest
{
    [Test]
    public void verify_when_no_arguments_passed_that_response_is_not_null()
    {
        StopThreadProfilerCommand command = new StopThreadProfilerCommand(new MockThreadProfilingService());
        object response = command.Process(null);
        Assert.That(response, Is.Not.Null);
    }

    [Test]
    public void verify_when_no_arguments_passed_that_response_is_expected_dictionary_type()
    {
        StopThreadProfilerCommand command = new StopThreadProfilerCommand(new MockThreadProfilingService());
        object response = command.Process(null);
        Dictionary<string, object> respDict = response as Dictionary<string, object>;
        Assert.That(respDict, Is.Not.Null);
    }

    [Test]
    public void verify_when_no_arguments_passed_that_response_dictionary_contains_single_entry()
    {
        StopThreadProfilerCommand command = new StopThreadProfilerCommand(new MockThreadProfilingService());
        object response = command.Process(null);
        Dictionary<string, object> respDict = response as Dictionary<string, object>;
        Assert.That(respDict, Has.Count.EqualTo(1));
    }

    [Test]
    public void verify_when_no_arguments_passed_that_response_contains_error()
    {
        StopThreadProfilerCommand command = new StopThreadProfilerCommand(new MockThreadProfilingService());
        object response = command.Process(null);
        Dictionary<string, object> respDict = response as Dictionary<string, object>;
        Assert.That(respDict.ContainsKey("error"), Is.True);
    }

    #region Profile Id Tests
    [Test]
    public void verify_when_profile_id_is_in_arguments_and_no_thread_profiling_session_is_not_currently_active_that_response_does_not_contain_an_error()
    {
        Dictionary<string, object> arguments = new Dictionary<string, object>();
        arguments.Add("profile_id", 4444);

        MockThreadProfilingService service = new MockThreadProfilingService();
        StopThreadProfilerCommand command = new StopThreadProfilerCommand(service);
        object response = command.Process(arguments);
        Dictionary<string, object> respDict = response as Dictionary<string, object>;
        Assert.That(respDict.ContainsKey("error"), Is.False);
    }

    [Test]
    public void verify_when_profile_id_is_in_arguments_and_a_thread_profiling_session_is_currently_active_that_response_does_not_contain_an_error()
    {
        Dictionary<string, object> arguments = new Dictionary<string, object>();
        arguments.Add("profile_id", 4444);

        MockThreadProfilingService service = new MockThreadProfilingService();
        StartThreadProfilerCommand startCmd = new StartThreadProfilerCommand(service);
        StopThreadProfilerCommand command = new StopThreadProfilerCommand(service);
        object response = command.Process(arguments);
        Dictionary<string, object> respDict = response as Dictionary<string, object>;
        Assert.That(respDict.ContainsKey("error"), Is.False);
    }

    [Test]
    public void verify_when_profile_id_is_missing_in_arguments_that_response_contains_an_error()
    {
        Dictionary<string, object> arguments = new Dictionary<string, object>();
        arguments.Add("report_data", true);

        MockThreadProfilingService service = new MockThreadProfilingService();
        StopThreadProfilerCommand command = new StopThreadProfilerCommand(service);
        object response = command.Process(arguments);
        Dictionary<string, object> respDict = response as Dictionary<string, object>;
        Assert.That(respDict.ContainsKey("error"), Is.True);
    }
    #endregion

    #region Report_Data Tests
    [Test]
    public void verify_when_report_data_is_missing_in_arguments_that_defaults_to_true()
    {
        Dictionary<string, object> arguments = new Dictionary<string, object>();
        arguments.Add("profile_id", 1234);

        MockThreadProfilingService service = new MockThreadProfilingService();
        StopThreadProfilerCommand command = new StopThreadProfilerCommand(service);
        object response = command.Process(arguments);
        Dictionary<string, object> respDict = response as Dictionary<string, object>;
        Assert.That(service.ReportData, Is.True);
    }

    [Test]
    public void verify_report_data_set_to_true_in_arguments_read_correctly()
    {
        Dictionary<string, object> arguments = new Dictionary<string, object>();
        arguments.Add("profile_id", 1234);
        arguments.Add("report_data", true);

        MockThreadProfilingService service = new MockThreadProfilingService();
        StopThreadProfilerCommand command = new StopThreadProfilerCommand(service);
        object response = command.Process(arguments);
        Dictionary<string, object> respDict = response as Dictionary<string, object>;
        Assert.That(service.ReportData, Is.True);
    }

    [Test]
    public void verify_report_data_set_to_false_in_arguments_read_correctly()
    {
        Dictionary<string, object> arguments = new Dictionary<string, object>();
        arguments.Add("profile_id", 1234);
        arguments.Add("report_data", false);

        MockThreadProfilingService service = new MockThreadProfilingService();
        StopThreadProfilerCommand command = new StopThreadProfilerCommand(service);
        object response = command.Process(arguments);
        Dictionary<string, object> respDict = response as Dictionary<string, object>;
        Assert.That(service.ReportData, Is.False);
    }

    #endregion

    #region Stop Result Mapping Tests

    private static StopThreadProfilerCommand CommandReturning(StopThreadProfilingSessionResult result)
    {
        var service = Mock.Create<IThreadProfilingSessionControl>();
        Mock.Arrange(() => service.StopThreadProfilingSession(Arg.AnyInt, Arg.IsAny<bool>())).Returns(result);
        return new StopThreadProfilerCommand(service);
    }

    [Test]
    public void verify_stopped_result_produces_no_error()
    {
        var arguments = new Dictionary<string, object> { { "profile_id", 1234 } };

        var command = CommandReturning(StopThreadProfilingSessionResult.Stopped);
        var respDict = command.Process(arguments) as Dictionary<string, object>;

        Assert.That(respDict.ContainsKey("error"), Is.False);
    }

    [Test]
    public void verify_not_running_result_produces_the_not_running_error()
    {
        var arguments = new Dictionary<string, object> { { "profile_id", 1234 } };

        var command = CommandReturning(StopThreadProfilingSessionResult.NotRunning);
        var respDict = command.Process(arguments) as Dictionary<string, object>;

        Assert.That(respDict["error"], Is.EqualTo("A thread profiling session is not running."));
    }

    [Test]
    public void verify_still_finishing_result_produces_a_distinct_message_not_the_not_running_one()
    {
        var arguments = new Dictionary<string, object> { { "profile_id", 1234 } };

        var command = CommandReturning(StopThreadProfilingSessionResult.StopRequestedWorkerStillFinishing);
        var respDict = command.Process(arguments) as Dictionary<string, object>;

        Assert.Multiple(() =>
        {
            Assert.That(respDict.ContainsKey("error"), Is.True);
            Assert.That(respDict["error"], Is.Not.EqualTo("A thread profiling session is not running."));
            Assert.That((string)respDict["error"], Does.Contain("still finishing"));
        });
    }

    #endregion
}