/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
using System.Collections.Generic;
using NewRelic.Agent.Core.ThreadProfiling;
using NUnit.Framework;

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
            Assert.IsNotNull(response);
        }

        [Test]
        public void verify_when_no_arguments_passed_that_response_is_expected_dictionary_type()
        {
            StartThreadProfilerCommand command = new StartThreadProfilerCommand(new MockThreadProfilingService());
            object response = command.Process(null);
            Dictionary<string, object> respDict = response as Dictionary<string, object>;
            Assert.IsNotNull(respDict);
        }

        [Test]
        public void verify_when_no_arguments_passed_that_response_dictionary_contains_single_entry()
        {
            StartThreadProfilerCommand command = new StartThreadProfilerCommand(new MockThreadProfilingService());
            object response = command.Process(null);
            Dictionary<string, object> respDict = response as Dictionary<string, object>;
            Assert.AreEqual(1, respDict.Count);
        }

        [Test]
        public void verify_when_no_arguments_passed_that_response_contains_error()
        {
            StartThreadProfilerCommand command = new StartThreadProfilerCommand(new MockThreadProfilingService());
            object response = command.Process(null);
            Dictionary<string, object> respDict = response as Dictionary<string, object>;
            Assert.IsTrue(respDict.ContainsKey("error"));
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
            Assert.IsFalse(respDict.ContainsKey("error"));
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
            Assert.IsTrue(respDict.ContainsKey("error"));
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
            Assert.AreEqual("A valid profile_id must be supplied to start a thread profiling session.", respDict["error"].ToString());
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
            Assert.AreEqual(ThreadProfilerCommandArgs.MinimumSamplingFrequencySeconds * 1000, service.Frequency);
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
            Assert.AreEqual(100, service.Frequency);
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
            Assert.AreEqual(910, service.Frequency);
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
            Assert.AreEqual(1000, service.Frequency);
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
            Assert.AreEqual(ThreadProfilerCommandArgs.MaximumSamplingFrequencySeconds * 1000, service.Frequency);
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
            Assert.AreEqual(ThreadProfilerCommandArgs.MaximumSamplingFrequencySeconds * 1000, service.Frequency);
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
            Assert.AreEqual(ThreadProfilerCommandArgs.DefaultSamplingFrequencySeconds * 1000, service.Frequency);
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
            Assert.AreEqual(ThreadProfilerCommandArgs.MinimumSamplingDurationSeconds * 1000, service.Duration);
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
            Assert.AreEqual(120000, service.Duration);
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
            Assert.AreEqual(600000, service.Duration);
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
            Assert.AreEqual(ThreadProfilerCommandArgs.MaximumSamplingDurationSeconds * 1000, service.Duration);
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
            Assert.AreEqual(ThreadProfilerCommandArgs.MaximumSamplingDurationSeconds * 1000, service.Duration);
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
            Assert.AreEqual(ThreadProfilerCommandArgs.DefaultSamplingDurationSeconds * 1000, service.Duration);
        }

        #endregion
    }
}
