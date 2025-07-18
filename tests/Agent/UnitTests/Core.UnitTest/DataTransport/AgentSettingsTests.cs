// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using Newtonsoft.Json;
using NUnit.Framework;
using NewRelic.Agent.Core.DataTransport;
using NewRelic.Agent.TestUtilities;
using Newtonsoft.Json.Converters;
using System.Collections.Generic;

namespace NewRelic.Agent.Core.Configuration
{
    [TestFixture]
    public class AgentSettingsTests
    {
        [Test]
        public void serializes_correctly()
        {
            var fullyPopulatedTestConfiguration = new ExhaustiveTestConfiguration();

            var agentSettings = new ReportedConfiguration(fullyPopulatedTestConfiguration);

            var json = JsonConvert.SerializeObject(agentSettings);

            const string expectedJson = """
                {
                    "agent.name": ".NET Agent",
                    "agent.run_id": "AgentRunId",
                    "agent.enabled": true,
                    "agent.license_key.configured": true,
                    "agent.application_names": ["name1", "name2", "name3"],
                    "agent.application_names_source": "ApplicationNameSource",
                    "agent.auto_start": true,
                    "browser_monitoring.application_id": "BrowserMonitoringApplicationId",
                    "browser_monitoring.auto_instrument": true,
                    "browser_monitoring.beacon_address": "BrowserMonitoringBeaconAddress",
                    "browser_monitoring.error_beacon_address": "BrowserMonitoringErrorBeaconAddress",
                    "browser_monitoring.javascript_agent.populated": true,
                    "browser_monitoring.javascript_agent_file": "BrowserMonitoringJavaScriptAgentFile",
                    "browser_monitoring.loader": "BrowserMonitoringJavaScriptAgentLoaderType",
                    "browser_monitoring.loader_debug": false,
                    "browser_monitoring.monitoring_key.populated": true,
                    "browser_monitoring.use_ssl": true,
                    "security.policies_token": "SecurityPoliciesToken",
                    "security.policies_token_exists": true,
                    "agent.allow_all_request_headers": true,
                    "agent.attributes_enabled": true,
                    "agent.can_use_attributes_includes": true,
                    "agent.can_use_attributes_includes_source": "CanUseAttributesIncludesSource",
                    "agent.attributes_include": ["include1", "include2", "include3"],
                    "agent.attributes_exclude": ["exclude1", "exclude2", "exclude3"],
                    "agent.attributes_default_excludes": ["defaultExclude1", "defaultExclude2", "defaultExclude3"],
                    "transaction_events.attributes_enabled": false,
                    "transaction_events.attributes_include": ["attributeInclude1", "attributeInclude2", "attributeInclude3"],
                    "transaction_events.attributes_exclude": ["attributeExclude1", "attributeExclude2", "attributeExclude3"],
                    "transaction_trace.attributes_enabled": true,
                    "transaction_trace.attributes_include": ["include1", "include2", "include3"],
                    "transaction_trace.attributes_exclude": ["exclude1", "exclude2", "exclude3"],
                    "error_collector.attributes_enabled": false,
                    "error_collector.attributes_include": ["include1", "include2", "include3"],
                    "error_collector.attributes_exclude": ["exclude1", "exclude2", "exclude3"],
                    "browser_monitoring.attributes_enabled": false,
                    "browser_monitoring.attributes_include": ["include1", "include2", "include3"],
                    "browser_monitoring.attributes_exclude": ["exclude1", "exclude2", "exclude3"],
                    "custom_parameters.enabled": false,
                    "custom_parameters.source": "CaptureCustomParametersSource",
                    "collector.host": "CollectorHost",
                    "collector.port": 1234,
                    "collector.send_data_on_exit": true,
                    "collector.send_data_on_exit_threshold": 4321.0,
                    "collector.send_environment_info": true,
                    "collector.sync_startup": true,
                    "collector.timeout": 1234,
                    "collector.max_payload_size_in_bytes": 4321,
                    "agent.complete_transactions_on_thread": true,
                    "agent.compressed_content_encoding": "CompressedContentEncoding",
                    "agent.configuration_version": 1234,
                    "cross_application_tracer.cross_process_id": "CrossApplicationTracingCrossProcessId",
                    "cross_application_tracer.enabled": true,
                    "distributed_tracing.enabled": true,
                    "distributed_tracing.sampler.remote_parent_sampled": "default",
                    "distributed_tracing.sampler.remote_parent_not_sampled": "default",
                    "span_events.enabled": true,
                    "span_events.harvest_cycle": "00:20:34",
                    "span_events.attributes_enabled": true,
                    "span_events.attributes_include": ["attributeInclude1", "attributeInclude2", "attributeInclude3"],
                    "span_events.attributes_exclude": ["attributeExclude1", "attributeExclude2", "attributeExclude3"],
                    "infinite_tracing.trace_count_consumers": 1234,
                    "infinite_tracing.trace_observer_host": "InfiniteTracingTraceObserverHost",
                    "infinite_tracing.trace_observer_port": "InfiniteTracingTraceObserverPort",
                    "infinite_tracing.trace_observer_ssl": "InfiniteTracingTraceObserverSsl",
                    "infinite_tracing.dev.test_flaky": 1234.0,
                    "infinite_tracing.dev.test_flaky_code": 4321,
                    "infinite_tracing.dev.test_delay_ms": 1234,
                    "infinite_tracing.spans_queue_size": 4321,
                    "infinite_tracing.spans_partition_count": 1234,
                    "infinite_tracing.spans_batch_size": 4321,
                    "infinite_tracing.connect_timeout_ms": 1234,
                    "infinite_tracing.send_data_timeout_ms": 4321,
                    "infinite_tracing.exit_timeout_ms": 1234,
                    "infinite_tracing.compression": true,
                    "agent.primary_application_id": "PrimaryApplicationId",
                    "agent.trusted_account_key": "TrustedAccountKey",
                    "agent.account_id": "AccountId",
                    "datastore_tracer.name_reporting_enabled": true,
                    "datastore_tracer.query_parameters_enabled": true,
                    "error_collector.enabled": true,
                    "error_collector.capture_events_enabled": true,
                    "error_collector.max_samples_stored": 1234,
                    "error_collector.harvest_cycle": "00:20:34",
                    "error_collector.max_per_period": 4321,
                    "error_collector.expected_classes": ["expected1", "expected2", "expected3"],
                    "error_collector.expected_messages": {
                        "first": ["first1", "first2"],
                        "second": ["second1", "second2"]
                    },
                    "error_collector.expected_status_codes": ["expectedError1", "expectedError2", "expectedError3"],
                    "error_collector.expected_errors_config": {
                        "third": ["third1", "third2"],
                        "fourth": ["fourth1", "fourth2"]
                    },
                    "error_collector.ignore_errors_config": {
                        "fifth": ["fifth1", "fifth2"],
                        "sixth": ["sixth1", "sixth2"]
                    },
                    "error_collector.ignore_classes": ["ignoreError1", "ignoreError2", "ignoreError3"],
                    "error_collector.ignore_messages": {
                        "seven": ["seven1", "seven2"],
                        "eight": ["eight1", "eight2"]
                    },
                    "agent.request_headers_map": {
                        "one": "1",
                        "two": "2"
                    },
                    "cross_application_tracer.encoding_key": "EncodingKey",
                    "agent.entity_guid": "EntityGuid",
                    "agent.high_security_mode_enabled": true,
                    "agent.custom_instrumentation_editor_enabled": true,
                    "agent.custom_instrumentation_editor_enabled_source": "CustomInstrumentationEditorEnabledSource",
                    "agent.strip_exception_messages": true,
                    "agent.strip_exception_messages_source": "StripExceptionMessagesSource",
                    "agent.instance_reporting_enabled": true,
                    "agent.instrumentation_logging_enabled": true,
                    "agent.labels": "Labels",
                    "agent.metric_name_regex_rules": [{
                            "MatchExpression": "match1",
                            "Replacement": "replacement1",
                            "Ignore": true,
                            "EvaluationOrder": 1,
                            "TerminateChain": true,
                            "EachSegment": true,
                            "ReplaceAll": true,
                            "MatchRegex": {
                                "Pattern": "match1",
                                "Options": 3
                            }
                        }, {
                            "MatchExpression": "match2",
                            "Replacement": "replacement2",
                            "Ignore": false,
                            "EvaluationOrder": 2,
                            "TerminateChain": false,
                            "EachSegment": false,
                            "ReplaceAll": false,
                            "MatchRegex": {
                                "Pattern": "match2",
                                "Options": 3
                            }
                        }
                    ],
                    "agent.new_relic_config_file_path": "NewRelicConfigFilePath",
                    "agent.app_settings_config_file_path": "AppSettingsConfigFilePath",
                    "proxy.host.configured": true,
                    "proxy.uri_path.configured": true,
                    "proxy.port.configured": true,
                    "proxy.username.configured": true,
                    "proxy.password.configured": true,
                    "proxy.domain.configured": true,
                    "agent.put_for_data_sent": true,
                    "slow_sql.enabled": true,
                    "transaction_tracer.explain_threshold": "00:20:34",
                    "transaction_tracer.explain_enabled": true,
                    "transaction_tracer.max_explain_plans": 1234,
                    "transaction_tracer.max_sql_statements": 4321,
                    "transaction_tracer.sql_traces_per_period": 1234,
                    "transaction_tracer.max_stack_trace_lines": 4321,
                    "error_collector.ignore_status_codes": ["ignore1", "ignore2", "ignore3"],
                    "agent.thread_profiling_methods_to_ignore": ["ignoreMethod1", "ignoreMethod2", "ignoreMethod3"],
                    "custom_events.enabled": true,
                    "custom_events.enabled_source": "CustomEventsEnabledSource",
                    "custom_events.attributes_enabled": true,
                    "custom_events.attributes_include": ["attributeInclude1", "attributeInclude2", "attributeInclude3"],
                    "custom_events.attributes_exclude": ["attributeExclude1", "attributeExclude2", "attributeExclude3"],
                    "custom_events.max_samples_stored": 1234,
                    "custom_events.harvest_cycle": "00:20:34",
                    "agent.disable_samplers": true,
                    "thread_profiler.enabled": true,
                    "transaction_events.enabled": true,
                    "transaction_events.max_samples_stored": 4321,
                    "transaction_events.harvest_cycle": "01:12:01",
                    "transaction_events.transactions_enabled": true,
                    "transaction_name.regex_rules": [{
                            "MatchExpression": "matchTrans1",
                            "Replacement": "replacementTrans1",
                            "Ignore": true,
                            "EvaluationOrder": 1,
                            "TerminateChain": true,
                            "EachSegment": true,
                            "ReplaceAll": true,
                            "MatchRegex": {
                                "Pattern": "matchTrans1",
                                "Options": 3
                            }
                        }, {
                            "MatchExpression": "matchTrans2",
                            "Replacement": "replacementTrans2",
                            "Ignore": false,
                            "EvaluationOrder": 2,
                            "TerminateChain": false,
                            "EachSegment": false,
                            "ReplaceAll": false,
                            "MatchRegex": {
                                "Pattern": "matchTrans2",
                                "Options": 3
                            }
                        }
                    ],
                    "transaction_name.whitelist_rules": {
                        "nine": ["nine1", "nine2"],
                        "ten": ["ten1", "ten2"]
                    },
                    "transaction_tracer.apdex_f": "00:20:34",
                    "transaction_tracer.apdex_t": "01:12:01",
                    "transaction_tracer.transaction_threshold": "00:20:34",
                    "transaction_tracer.enabled": true,
                    "transaction_tracer.max_segments": 1234,
                    "transaction_tracer.record_sql": "TransactionTracerRecordSql",
                    "transaction_tracer.record_sql_source": "TransactionTracerRecordSqlSource",
                    "transaction_tracer.max_stack_traces": 4321,
                    "agent.trusted_account_ids": [1, 2, 3],
                    "agent.server_side_config_enabled": true,
                    "agent.ignore_server_side_config": true,
                    "agent.url_regex_rules": [{
                            "MatchExpression": "matchUrl1",
                            "Replacement": "replacementUrl1",
                            "Ignore": true,
                            "EvaluationOrder": 1,
                            "TerminateChain": true,
                            "EachSegment": true,
                            "ReplaceAll": true,
                            "MatchRegex": {
                                "Pattern": "matchUrl1",
                                "Options": 3
                            }
                        }, {
                            "MatchExpression": "matchUrl2",
                            "Replacement": "replacementUrl2",
                            "Ignore": false,
                            "EvaluationOrder": 2,
                            "TerminateChain": false,
                            "EachSegment": false,
                            "ReplaceAll": false,
                            "MatchRegex": {
                                "Pattern": "matchUrl2",
                                "Options": 3
                            }
                        }
                    ],
                    "agent.request_path_exclusion_list": [{
                            "Pattern": "asdf",
                            "Options": 0
                        }, {
                            "Pattern": "qwerty",
                            "Options": 1
                        }, {
                            "Pattern": "yolo",
                            "Options": 4
                        }
                    ],
                    "agent.web_transactions_apdex": {
                        "first": 1.0,
                        "second": 2.0
                    },
                    "agent.wrapper_exception_limit": 1234,
                    "utilization.detect_aws_enabled": true,
                    "utilization.detect_azure_enabled": true,
                    "utilization.detect_gcp_enabled": true,
                    "utilization.detect_pcf_enabled": true,
                    "utilization.detect_docker_enabled": true,
                    "utilization.detect_kubernetes_enabled": true,
                    "utilization.detect_azure_function_enabled": true,
                    "utilization.detect_azure_appservice_enabled": true,

                    "utilization.logical_processors": 22,
                    "utilization.total_ram_mib": 33,
                    "utilization.billing_host": "UtilizationBillingHost",
                    "utilization.hostname": "UtilizationHostName",
                    "utilization.full_hostname": "UtilizationFullHostName",
                    "diagnostics.capture_agent_timing_enabled": true,
                    "diagnostics.capture_agent_timing_frequency": 1234,
                    "agent.use_resource_based_naming_for_wcf_enabled": true,
                    "agent.event_listener_samplers_enabled": true,
                    "agent.sampling_target": 1234,
                    "span_events.max_samples_stored": 4321,
                    "agent.sampling_target_period_in_seconds": 1234,
                    "agent.payload_success_metrics_enabled": true,
                    "agent.process_host_display_name": "ProcessHostDisplayName",
                    "transaction_tracer.database_statement_cache_capacity": 1234,
                    "agent.force_synchronous_timing_calculation_for_http_client": true,
                    "agent.enable_asp_net_core_6plus_browser_injection": true,
                    "agent.exclude_new_relic_header": true,
                    "application_logging.enabled": true,
                    "application_logging.metrics.enabled": true,
                    "application_logging.forwarding.enabled": true,
                    "application_logging.forwarding.max_samples_stored": 1234,
                    "application_logging.forwarding.log_level_denylist": ["testlevel1, testlevel2"],
                    "application_logging.harvest_cycle": "00:20:34",
                    "application_logging.local_decorating.enabled": true,
                    "agent.app_domain_caching_disabled": true,
                    "agent.force_new_transaction_on_new_thread_enabled": true,
                    "agent.code_level_metrics_enabled": true,
                    "agent.app_settings": {
                        "hello": "friend",
                        "we": "made",
                        "it": "to",
                        "the": "end"
                    },
                    "application_logging.forwarding.context_data.enabled": true,
                    "application_logging.forwarding.context_data.include": ["attr1", "attr2"],
                    "application_logging.forwarding.context_data.exclude": ["attr1", "attr2"],
                    "application_logging.forwarding.labels.enabled": true,
                    "application_logging.forwarding.labels.exclude": ["label1", "label2"],
                    "metrics.harvest_cycle": "00:01:00",
                    "transaction_traces.harvest_cycle": "00:01:00",
                    "error_traces.harvest_cycle": "00:01:00",
                    "get_agent_commands.cycle": "00:01:00",
                    "default.harvest_cycle": "00:01:00",
                    "sql_traces.harvest_cycle": "00:01:00",
                    "update_loaded_modules.cycle": "00:01:00",
                    "stackexchangeredis_cleanup.cycle": "00:01:00",
                    "agent.logging_enabled": true,
                    "agent.instrumentation.ignore": [{
                            "assemblyName": "AssemblyToIgnore1"
                        }, {
                            "assemblyName": "AssemblyToIgnore2",
                            "className": "ClassNameToIgnore"
                        }
                    ],
                    "agent.disable_file_system_watcher": false,
                    "ai_monitoring.enabled": true,
                    "ai_monitoring.streaming.enabled": true,
                    "ai_monitoring.record_content.enabled": true,
                    "gc_sampler_v2.enabled": true,
                    "agent_control.enabled" : true,
                    "agent_control.health.delivery_location": "file:///tmp/health",
                    "agent_control.health.frequency": 5,
                    "otel_bridge.included_activity_sources": ["SomeIncludedActivitySourceName","AnotherIncludedActivitySourceName"],
                    "otel_bridge.excluded_activity_sources": ["SomeExcludedActivitySourceName","AnotherExcludedActivitySourceName"],
                    "otel_bridge.enabled": true
                }
                """;

            Assert.Multiple(() =>
            {
                Assert.That(json, Is.EqualTo(expectedJson.Condense()));

                // Confirm that JsonIgnored properties are present, but not serialized
                Assert.That(agentSettings.AgentLicenseKey, Is.Not.Null);
                Assert.That(agentSettings.BrowserMonitoringJavaScriptAgent, Is.Not.Null);
                Assert.That(agentSettings.BrowserMonitoringKey, Is.Not.Null);
                Assert.That(agentSettings.ErrorGroupCallback, Is.Not.Null);
                Assert.That(agentSettings.LlmTokenCountingCallback, Is.Not.Null);
                Assert.That(agentSettings.AgentEnabledAt, Is.Not.Null);
                Assert.That(agentSettings.ServerlessModeEnabled, Is.False);
                Assert.That(agentSettings.LoggingLevel, Is.Not.Null);
                Assert.That(agentSettings.ServerlessFunctionName, Is.Null);
                Assert.That(agentSettings.ServerlessFunctionVersion, Is.Null);
                Assert.That(agentSettings.AwsAccountId, Is.Empty);
            });
        }
    }
}
