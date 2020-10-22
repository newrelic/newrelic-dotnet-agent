// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Configuration;
using Newtonsoft.Json;
using NUnit.Framework;
using System.Linq;
using Telerik.JustMock;
using System.Collections.Generic;
using System;

namespace NewRelic.Agent.Core.Configuration
{
    [TestFixture]
    public class AgentSettingsTests
    {
        TimeSpan ApdexT = new TimeSpan(0, 0, 10);
        const string CatId = "acctId#appId";
        const string EncodingKey = "thisistheencodingkey";
        List<long> TrustedAccountIds = new List<long> { 123456, 098765 };
        const int MaxStackTraceLines = 100;
        const bool UsingServerSideConfig = false;
        const bool ThreadProfilerEnabled = false;
        const bool CrossApplicationTracerEnabled = false;
        const bool DistributedTracingEnabled = true;
        const bool ErrorCollectorEnabled = true;
        List<string> ErrorCollectorIgnoreStatusCodes = new List<string> { "401", "404" };
        List<string> ErrorCollectorIgnoreErrors = new List<string>();
        List<string> ErrorCollectorExpectedClasses = new List<string> { "ExceptionClass1" };
        IDictionary<string, IEnumerable<string>> ErrorCollectorExpectedMessages = new Dictionary<string, IEnumerable<string>>
            {
                { "ExceptionClass2", new [] { "exception message 1" } }
            };
        List<string> ErrorCollectorIgnoreClasses = new List<string> { "ExceptionClass1" };
        IDictionary<string, IEnumerable<string>> ErrorCollectorIgnoreMessages = new Dictionary<string, IEnumerable<string>>
            {
                { "ExceptionClass2", new [] { "exception message 1" } }
            };
        readonly string[] ErrorCollectorExpectedStatusCodes = { "403", "500" };
        TimeSpan TransactionTracerStackThreshold = new TimeSpan(0, 0, 11);
        const bool TransactionTracerExplainEnabled = false;
        TimeSpan TransactionTracerExplainThreshold = new TimeSpan(0, 0, 12);
        const uint MaxSqlStatements = 100;
        const int MaxExplainPlans = 10;
        TimeSpan TransactionTracerThreshold = new TimeSpan(0, 0, 13);
        const string TransactionTracerRecordSql = "obfuscate";
        const bool SlowSqlEnabled = false;
        const bool BrowserMonitoringAutoInstrument = true;
        const int TransactionEventMaxSamplesStored = 10000;

        [Test]
        public void serializes_correctly()
        {
            var configuration = Mock.Create<IConfiguration>();
            Mock.Arrange(() => configuration.TransactionTraceApdexT).Returns(ApdexT);
            Mock.Arrange(() => configuration.CrossApplicationTracingCrossProcessId).Returns(CatId);
            Mock.Arrange(() => configuration.EncodingKey).Returns(EncodingKey);
            Mock.Arrange(() => configuration.TrustedAccountIds).Returns(TrustedAccountIds);
            Mock.Arrange(() => configuration.StackTraceMaximumFrames).Returns(MaxStackTraceLines);
            Mock.Arrange(() => configuration.UsingServerSideConfig).Returns(UsingServerSideConfig);
            Mock.Arrange(() => configuration.ThreadProfilingEnabled).Returns(ThreadProfilerEnabled);
            Mock.Arrange(() => configuration.CrossApplicationTracingEnabled).Returns(CrossApplicationTracerEnabled);
            Mock.Arrange(() => configuration.DistributedTracingEnabled).Returns(DistributedTracingEnabled);
            Mock.Arrange(() => configuration.ErrorCollectorEnabled).Returns(ErrorCollectorEnabled);
            Mock.Arrange(() => configuration.ExpectedErrorClassesForAgentSettings).Returns(ErrorCollectorExpectedClasses);
            Mock.Arrange(() => configuration.ExpectedErrorMessagesForAgentSettings).Returns(ErrorCollectorExpectedMessages);
            Mock.Arrange(() => configuration.ExpectedErrorStatusCodesForAgentSettings).Returns(ErrorCollectorExpectedStatusCodes);
            Mock.Arrange(() => configuration.HttpStatusCodesToIgnore).Returns(ErrorCollectorIgnoreStatusCodes);
            Mock.Arrange(() => configuration.IgnoreErrorsForAgentSettings).Returns(ErrorCollectorIgnoreStatusCodes);
            Mock.Arrange(() => configuration.IgnoreErrorClassesForAgentSettings).Returns(ErrorCollectorIgnoreClasses);
            Mock.Arrange(() => configuration.IgnoreErrorMessagesForAgentSettings).Returns(ErrorCollectorIgnoreMessages);
            Mock.Arrange(() => configuration.TransactionTracerStackThreshold).Returns(TransactionTracerStackThreshold);
            Mock.Arrange(() => configuration.SqlExplainPlansEnabled).Returns(TransactionTracerExplainEnabled);
            Mock.Arrange(() => configuration.SqlExplainPlanThreshold).Returns(TransactionTracerExplainThreshold);
            Mock.Arrange(() => configuration.SqlStatementsPerTransaction).Returns(MaxSqlStatements);
            Mock.Arrange(() => configuration.SqlExplainPlansMax).Returns(MaxExplainPlans);
            Mock.Arrange(() => configuration.TransactionTraceThreshold).Returns(TransactionTracerThreshold);
            Mock.Arrange(() => configuration.TransactionTracerRecordSql).Returns(TransactionTracerRecordSql);
            Mock.Arrange(() => configuration.SlowSqlEnabled).Returns(SlowSqlEnabled);
            Mock.Arrange(() => configuration.BrowserMonitoringAutoInstrument).Returns(BrowserMonitoringAutoInstrument);
            Mock.Arrange(() => configuration.TransactionEventsMaximumSamplesStored).Returns(TransactionEventMaxSamplesStored);

            var agentSettings = new ReportedConfiguration
            {
                ApdexT = configuration.TransactionTraceApdexT.TotalSeconds,
                CatId = configuration.CrossApplicationTracingCrossProcessId,
                EncodingKey = configuration.EncodingKey,
                TrustedAccountIds = configuration.TrustedAccountIds.ToList(),
                MaxStackTraceLines = configuration.StackTraceMaximumFrames,
                UsingServerSideConfig = configuration.UsingServerSideConfig,
                ThreadProfilerEnabled = configuration.ThreadProfilingEnabled,
                CrossApplicationTracerEnabled = configuration.CrossApplicationTracingEnabled,
                DistributedTracingEnabled = configuration.DistributedTracingEnabled,
                ErrorCollectorEnabled = configuration.ErrorCollectorEnabled,
                ErrorCollectorIgnoreStatusCodes = configuration.HttpStatusCodesToIgnore.ToList(),
                ErrorCollectorIgnoreErrors = configuration.IgnoreErrorsForAgentSettings.ToList(),
                ErrorCollectorIgnoreClasses = configuration.IgnoreErrorClassesForAgentSettings,
                ErrorCollectorIgnoreMessages = configuration.IgnoreErrorMessagesForAgentSettings,
                ErrorCollectorExpectedClasses = configuration.ExpectedErrorClassesForAgentSettings,
                ErrorCollectorExpectedMessages = configuration.ExpectedErrorMessagesForAgentSettings,
                ErrorCollectorExpectedStatusCodes = configuration.ExpectedErrorStatusCodesForAgentSettings,
                TransactionTracerStackThreshold = configuration.TransactionTracerStackThreshold.TotalSeconds,
                TransactionTracerExplainEnabled = configuration.SqlExplainPlansEnabled,
                TransactionTracerExplainThreshold = configuration.SqlExplainPlanThreshold.TotalSeconds,
                MaxSqlStatements = configuration.SqlStatementsPerTransaction,
                MaxExplainPlans = configuration.SqlExplainPlansMax,
                TransactionTracerThreshold = configuration.TransactionTraceThreshold.TotalSeconds,
                TransactionTracerRecordSql = configuration.TransactionTracerRecordSql,
                SlowSqlEnabled = configuration.SlowSqlEnabled,
                BrowserMonitoringAutoInstrument = configuration.BrowserMonitoringAutoInstrument,
                TransactionEventMaxSamplesStored = configuration.TransactionEventsMaximumSamplesStored
            };

            var json = JsonConvert.SerializeObject(agentSettings);

            const string expectedJson = @"{""apdex_t"":10.0,""cross_process_id"":""acctId#appId"",""encoding_key"":""thisistheencodingkey"",""trusted_account_ids"":[123456,98765],""max_stack_trace_lines"":100,""using_server_side_config"":false,""thread_profiler.enabled"":false,""cross_application_tracer.enabled"":false,""distributed_tracing.enabled"":true,""error_collector.enabled"":true,""error_collector.ignore_status_codes"":[""401"",""404""],""error_collector.ignore_errors"":[""401"",""404""],""error_collector.ignore_classes"":[""ExceptionClass1""],""error_collector.ignore_messages"":{""ExceptionClass2"":[""exception message 1""]},""error_collector.expected_classes"":[""ExceptionClass1""],""error_collector.expected_messages"":{""ExceptionClass2"":[""exception message 1""]},""error_collector.expected_status_codes"":[""403"",""500""],""transaction_tracer.stack_trace_threshold"":11.0,""transaction_tracer.explain_enabled"":false,""transaction_tracer.max_sql_statements"":100,""transaction_tracer.max_explain_plans"":10,""transaction_tracer.explain_threshold"":12.0,""transaction_tracer.transaction_threshold"":13.0,""transaction_tracer.record_sql"":""obfuscate"",""slow_sql.enabled"":false,""browser_monitoring.auto_instrument"":true,""transaction_event.max_samples_stored"":10000}";

            Assert.AreEqual(expectedJson, json);
        }
    }
}
