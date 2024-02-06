// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using NUnit.Framework;
using NewRelic.Agent.Core.ThreadProfiling;

namespace NewRelic.Agent.Core.Commands
{
    [TestFixture]
    public class StartThreadProfilingCommandTest
    {
        [Test]
        public void verify_when_no_arguments_passed_that_response_is_not_null()
        {
            StartThreadProfilerCommand command = new StartThreadProfilerCommand(new MockThreadProfilingService());
            object response = command.Process(null);
            Assert.That(response, Is.Not.Null);
        }

        [Test]
        public void verify_when_no_arguments_passed_that_response_is_expected_dictionary_type()
        {
            StartThreadProfilerCommand command = new StartThreadProfilerCommand(new MockThreadProfilingService());
            object response = command.Process(null);
            Dictionary<string, object> respDict = response as Dictionary<string, object>;
            Assert.That(respDict, Is.Not.Null);
        }

        [Test]
        public void verify_when_no_arguments_passed_that_response_dictionary_contains_single_entry()
        {
            StartThreadProfilerCommand command = new StartThreadProfilerCommand(new MockThreadProfilingService());
            object response = command.Process(null);
            Dictionary<string, object> respDict = response as Dictionary<string, object>;
            Assert.That(respDict, Has.Count.EqualTo(1));
        }

        [Test]
        public void verify_when_no_arguments_passed_that_response_contains_error()
        {
            StartThreadProfilerCommand command = new StartThreadProfilerCommand(new MockThreadProfilingService());
            object response = command.Process(null);
            Dictionary<string, object> respDict = response as Dictionary<string, object>;
            Assert.That(respDict.ContainsKey("error"), Is.True);
        }

        #region Profile Id Tests
        [Test]
        public void verify_when_profile_id_is_in_arguments_that_response_does_not_contain_an_error()
        {
            Dictionary<string, object> arguments = new Dictionary<string, object>();
            arguments.Add("profile_id", 4444);
            arguments.Add("sample_period", 0.1);
            arguments.Add("duration", 180.0);
            arguments.Add("only_runnable_threads", false);
            arguments.Add("only_request_threads", false);
            arguments.Add("profile_agent_code", false);

            StartThreadProfilerCommand command = new StartThreadProfilerCommand(new MockThreadProfilingService());
            object response = command.Process(arguments);
            Dictionary<string, object> respDict = response as Dictionary<string, object>;
            Assert.That(respDict.ContainsKey("error"), Is.False);
        }

        [Test]
        public void verify_when_profile_id_not_in_arguments_that_response_contains_an_error()
        {
            Dictionary<string, object> arguments = new Dictionary<string, object>();
            arguments.Add("sample_period", 0.1);
            arguments.Add("duration", 180.0);
            arguments.Add("only_runnable_threads", false);
            arguments.Add("only_request_threads", false);
            arguments.Add("profile_agent_code", false);

            StartThreadProfilerCommand command = new StartThreadProfilerCommand(new MockThreadProfilingService());
            object response = command.Process(arguments);
            Dictionary<string, object> respDict = response as Dictionary<string, object>;
            Assert.That(respDict.ContainsKey("error"), Is.True);
        }

        [Test]
        public void verify_when_profile_id_not_in_arguments_that_response_contains_correct_error_message()
        {
            Dictionary<string, object> arguments = new Dictionary<string, object>();
            arguments.Add("sample_period", 0.1);
            arguments.Add("duration", 180.0);
            arguments.Add("only_runnable_threads", false);
            arguments.Add("only_request_threads", false);
            arguments.Add("profile_agent_code", false);

            StartThreadProfilerCommand command = new StartThreadProfilerCommand(new MockThreadProfilingService());
            object response = command.Process(arguments);
            Dictionary<string, object> respDict = response as Dictionary<string, object>;
            Assert.That(respDict["error"].ToString(), Is.EqualTo("A valid profile_id must be supplied to start a thread profiling session."));
        }
        #endregion

        #region Sampling Frequency Tests

        [Test]
        public void verify_sample_period_argument_equal_to_less_than_minimum_sampling_rate_converts_to_minimum_frequency_in_msec()
        {
            Dictionary<string, object> arguments = new Dictionary<string, object>();
            arguments.Add("profile_id", 4444);
            arguments.Add("sample_period", 0.09);
            arguments.Add("duration", 180.0);
            arguments.Add("only_runnable_threads", false);
            arguments.Add("only_request_threads", false);
            arguments.Add("profile_agent_code", false);

            MockThreadProfilingService service = new MockThreadProfilingService();
            StartThreadProfilerCommand command = new StartThreadProfilerCommand(service);
            object response = command.Process(arguments);
            Assert.That(service.Frequency, Is.EqualTo(ThreadProfilerCommandArgs.MinimumSamplingFrequencySeconds * 1000));
        }

        [Test]
        public void verify_sample_period_argument_equal_to_1_tenth_of_second_converts_correctly_to_frequency_in_msec()
        {
            Dictionary<string, object> arguments = new Dictionary<string, object>();
            arguments.Add("profile_id", 4444);
            arguments.Add("sample_period", 0.1);
            arguments.Add("duration", 180.0);
            arguments.Add("only_runnable_threads", false);
            arguments.Add("only_request_threads", false);
            arguments.Add("profile_agent_code", false);

            MockThreadProfilingService service = new MockThreadProfilingService();
            StartThreadProfilerCommand command = new StartThreadProfilerCommand(service);
            object response = command.Process(arguments);
            Assert.That(service.Frequency, Is.EqualTo(100));
        }

        [Test]
        public void verify_sample_period_argument_equal_to_less_than_1_second_sample_converts_correctly_to_frequency_in_msec()
        {
            Dictionary<string, object> arguments = new Dictionary<string, object>();
            arguments.Add("profile_id", 4444);
            arguments.Add("sample_period", 0.91);
            arguments.Add("duration", 180.0);
            arguments.Add("only_runnable_threads", false);
            arguments.Add("only_request_threads", false);
            arguments.Add("profile_agent_code", false);

            MockThreadProfilingService service = new MockThreadProfilingService();
            StartThreadProfilerCommand command = new StartThreadProfilerCommand(service);
            object response = command.Process(arguments);
            Assert.That(service.Frequency, Is.EqualTo(910));
        }

        [Test]
        public void verify_sample_period_argument_equal_to_1_second_converts_correctly_to_frequency_in_msec()
        {
            Dictionary<string, object> arguments = new Dictionary<string, object>();
            arguments.Add("profile_id", 4444);
            arguments.Add("sample_period", 1.00);
            arguments.Add("duration", 180.0);
            arguments.Add("only_runnable_threads", false);
            arguments.Add("only_request_threads", false);
            arguments.Add("profile_agent_code", false);

            MockThreadProfilingService service = new MockThreadProfilingService();
            StartThreadProfilerCommand command = new StartThreadProfilerCommand(service);
            object response = command.Process(arguments);
            Assert.That(service.Frequency, Is.EqualTo(1000));
        }

        [Test]
        public void verify_sample_period_argument_greater_than_maximum_sampling_rate_converts_to_max_frequency_in_msec()
        {
            Dictionary<string, object> arguments = new Dictionary<string, object>();
            arguments.Add("profile_id", 4444);
            arguments.Add("sample_period", ThreadProfilerCommandArgs.MaximumSamplingFrequencySeconds + 1);
            arguments.Add("duration", 180.0);
            arguments.Add("only_runnable_threads", false);
            arguments.Add("only_request_threads", false);
            arguments.Add("profile_agent_code", false);

            MockThreadProfilingService service = new MockThreadProfilingService();
            StartThreadProfilerCommand command = new StartThreadProfilerCommand(service);
            object response = command.Process(arguments);
            Assert.That(service.Frequency, Is.EqualTo(ThreadProfilerCommandArgs.MaximumSamplingFrequencySeconds * 1000));
        }

        [Test]
        public void verify_sample_period_argument_overflow_converts_to_max_frequency_in_msec()
        {
            Dictionary<string, object> arguments = new Dictionary<string, object>();
            arguments.Add("profile_id", 4444);
            arguments.Add("sample_period", 999999999999999999);
            arguments.Add("duration", 180.0);
            arguments.Add("only_runnable_threads", false);
            arguments.Add("only_request_threads", false);
            arguments.Add("profile_agent_code", false);

            MockThreadProfilingService service = new MockThreadProfilingService();
            StartThreadProfilerCommand command = new StartThreadProfilerCommand(service);
            object response = command.Process(arguments);
            Assert.That(service.Frequency, Is.EqualTo(ThreadProfilerCommandArgs.MaximumSamplingFrequencySeconds * 1000));
        }

        [Test]
        public void verify_if_sample_period_argument_missing_that_max_frequency_in_msec_set_to_default()
        {
            Dictionary<string, object> arguments = new Dictionary<string, object>();
            arguments.Add("profile_id", 4444);
            arguments.Add("duration", 180.0);
            arguments.Add("only_runnable_threads", false);
            arguments.Add("only_request_threads", false);
            arguments.Add("profile_agent_code", false);

            MockThreadProfilingService service = new MockThreadProfilingService();
            StartThreadProfilerCommand command = new StartThreadProfilerCommand(service);
            object response = command.Process(arguments);
            Assert.That(service.Frequency, Is.EqualTo(ThreadProfilerCommandArgs.DefaultSamplingFrequencySeconds * 1000));
        }
        #endregion

        #region Sampling Duration Tests

        [Test]
        public void verify_duration_argument_equal_to_less_than_minimum_duration_converts_to_minimum_duration_in_msec()
        {
            Dictionary<string, object> arguments = new Dictionary<string, object>();
            arguments.Add("profile_id", 4444);
            arguments.Add("sample_period", 0.1);
            arguments.Add("duration", 4.9);
            arguments.Add("only_runnable_threads", false);
            arguments.Add("only_request_threads", false);
            arguments.Add("profile_agent_code", false);

            MockThreadProfilingService service = new MockThreadProfilingService();
            StartThreadProfilerCommand command = new StartThreadProfilerCommand(service);
            object response = command.Process(arguments);
            Assert.That(service.Duration, Is.EqualTo(ThreadProfilerCommandArgs.MinimumSamplingDurationSeconds * 1000));
        }

        [Test]
        public void verify_duration_argument_equal_to_120_seconds_converts_correctly_to_duration_in_msec()
        {
            Dictionary<string, object> arguments = new Dictionary<string, object>();
            arguments.Add("profile_id", 4444);
            arguments.Add("sample_period", 0.1);
            arguments.Add("duration", 120.00);
            arguments.Add("only_runnable_threads", false);
            arguments.Add("only_request_threads", false);
            arguments.Add("profile_agent_code", false);

            MockThreadProfilingService service = new MockThreadProfilingService();
            StartThreadProfilerCommand command = new StartThreadProfilerCommand(service);
            object response = command.Process(arguments);
            Assert.That(service.Duration, Is.EqualTo(120000));
        }


        [Test]
        public void verify_duration_argument_equal_to_10_minutes_converts_correctly_to_duration_in_msec()
        {
            Dictionary<string, object> arguments = new Dictionary<string, object>();
            arguments.Add("profile_id", 4444);
            arguments.Add("sample_period", 0.1);
            arguments.Add("duration", 600.00);
            arguments.Add("only_runnable_threads", false);
            arguments.Add("only_request_threads", false);
            arguments.Add("profile_agent_code", false);

            MockThreadProfilingService service = new MockThreadProfilingService();
            StartThreadProfilerCommand command = new StartThreadProfilerCommand(service);
            object response = command.Process(arguments);
            Assert.That(service.Duration, Is.EqualTo(600000));
        }

        [Test]
        public void verify_duration_argument_greater_than_maximum_duration_converts_to_max_duration_in_msec()
        {
            Dictionary<string, object> arguments = new Dictionary<string, object>();
            arguments.Add("profile_id", 4444);
            arguments.Add("sample_period", 0.1);
            arguments.Add("duration", ThreadProfilerCommandArgs.MaximumSamplingDurationSeconds + 1);
            arguments.Add("only_runnable_threads", false);
            arguments.Add("only_request_threads", false);
            arguments.Add("profile_agent_code", false);

            MockThreadProfilingService service = new MockThreadProfilingService();
            StartThreadProfilerCommand command = new StartThreadProfilerCommand(service);
            object response = command.Process(arguments);
            Assert.That(service.Duration, Is.EqualTo(ThreadProfilerCommandArgs.MaximumSamplingDurationSeconds * 1000));
        }

        [Test]
        public void verify_duration_argument_overflow_converts_to_max_duration_in_msec()
        {
            Dictionary<string, object> arguments = new Dictionary<string, object>();
            arguments.Add("profile_id", 4444);
            arguments.Add("sample_period", 0.1);
            arguments.Add("duration", 999999999999999999);
            arguments.Add("only_runnable_threads", false);
            arguments.Add("only_request_threads", false);
            arguments.Add("profile_agent_code", false);

            MockThreadProfilingService service = new MockThreadProfilingService();
            StartThreadProfilerCommand command = new StartThreadProfilerCommand(service);
            object response = command.Process(arguments);
            Assert.That(service.Duration, Is.EqualTo(ThreadProfilerCommandArgs.MaximumSamplingDurationSeconds * 1000));
        }

        [Test]
        public void verify_if_duration_argument_missing_that_max_duration_in_msec_set_to_default()
        {
            Dictionary<string, object> arguments = new Dictionary<string, object>();
            arguments.Add("profile_id", 4444);
            arguments.Add("sample_period", 0.1);
            arguments.Add("only_runnable_threads", false);
            arguments.Add("only_request_threads", false);
            arguments.Add("profile_agent_code", false);

            MockThreadProfilingService service = new MockThreadProfilingService();
            StartThreadProfilerCommand command = new StartThreadProfilerCommand(service);
            object response = command.Process(arguments);
            Assert.That(service.Duration, Is.EqualTo(ThreadProfilerCommandArgs.DefaultSamplingDurationSeconds * 1000));
        }

        #endregion
    }
}
