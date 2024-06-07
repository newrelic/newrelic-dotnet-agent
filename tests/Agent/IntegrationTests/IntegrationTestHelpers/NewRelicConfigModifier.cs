// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;

namespace NewRelic.Agent.IntegrationTestHelpers
{
    public class NewRelicConfigModifier
    {
        private readonly string _configFilePath;

        public NewRelicConfigModifier(string configFilePath)
        {
            _configFilePath = configFilePath;
        }

        public void SetLicenseKey(string key)
        {
            CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(_configFilePath, new[] { "configuration", "service" },
                "licenseKey", key);
        }

        public void SetSecurityToken(string token)
        {
            CommonUtils.ModifyOrCreateXmlNodeInNewRelicConfig(_configFilePath, new[] { "configuration" }, "securityPoliciesToken",
                token);
        }

        public void SetHighSecurityMode(bool value)
        {
            CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(_configFilePath, new[] { "configuration", "highSecurity" },
                "enabled", value.ToString().ToLower());
        }

        public void AddAttributesInclude(string include)
        {
            CommonUtils.AddXmlNodeInNewRelicConfig(_configFilePath, new[] { "configuration", "attributes" }, "include",
                include);
        }

        public void AddAttributesExclude(string exclude)
        {
            CommonUtils.AddXmlNodeInNewRelicConfig(_configFilePath, new[] { "configuration", "attributes" }, "exclude",
                exclude);
        }

        public void SetAutoStart(bool value)
        {
            CommonUtils.ModifyOrCreateXmlAttributesInNewRelicConfig(_configFilePath, new[] { "configuration", "service" },
                new[] { new KeyValuePair<string, string>("autoStart", value.ToString().ToLower()) });
        }

        public void SetTransactionTracerRecordSql(string value)
        {
            CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(_configFilePath, new[] { "configuration", "transactionTracer" },
                "recordSql", value);
        }

        public void SetHost(string host)
        {
            CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(_configFilePath, new[] { "configuration", "service" }, "host",
                host);
        }

        public void SetHostPort(int port)
        {
            CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(_configFilePath, new[] { "configuration", "service" }, "port",
                port.ToString());
        }

        public void SetRequestTimeout(TimeSpan duration)
        {
            CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(_configFilePath, new[] { "configuration", "service" }, "requestTimeout",
                duration.TotalMilliseconds.ToString());
        }


        public NewRelicConfigModifier ForceTransactionTraces()
        {
            CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(_configFilePath, new[] { "configuration", "transactionTracer" },
                "transactionThreshold", "1");
            return this;
        }

        public NewRelicConfigModifier AddExpectedErrorMessages(string errorClassName, IEnumerable<string> messages)
        {
            CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(_configFilePath, new[] { "configuration", "errorCollector", "expectedMessages", "errorClass" }, "name", errorClassName);

            foreach (var message in messages)
            {
                CommonUtils.AddXmlNodeInNewRelicConfig(_configFilePath, new[] { "configuration", "errorCollector", "expectedMessages", "errorClass" }, "message", message);
            }
            return this;
        }

        public NewRelicConfigModifier AddExpectedErrorClasses(IEnumerable<string> errorClasses)
        {
            foreach (var className in errorClasses)
            {
                CommonUtils.AddXmlNodeInNewRelicConfig(_configFilePath, new[] { "configuration", "errorCollector", "expectedClasses" }, "errorClass", className);
            }
            return this;
        }

        public NewRelicConfigModifier AddExpectedStatusCodes(string statusCodes)
        {
            CommonUtils.AddXmlNodeInNewRelicConfig(_configFilePath, new[] { "configuration", "errorCollector"}, "expectedStatusCodes", statusCodes );
            return this;
        }

        public NewRelicConfigModifier EnableDistributedTrace()
        {
            SetOrDeleteDistributedTraceEnabled(true);
            return this;
        }

        public NewRelicConfigModifier EnableInfiniteTracing(string traceObserverUrl, string traceObserverPort)
        {
            CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(_configFilePath,
               new[] { "configuration", "infiniteTracing", "trace_observer" }, "host", traceObserverUrl);
            CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(_configFilePath,
                    new[] { "configuration", "infiniteTracing", "trace_observer" }, "port", traceObserverPort);
            return this;
        }

        public void AutoInstrumentBrowserMonitoring(bool shouldAutoInstrument)
        {
            var stringValue = shouldAutoInstrument.ToString().ToLower(); //We don't seem to handle the uppercase parse
            CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(_configFilePath, new[] { "configuration", "browserMonitoring" },
                "autoInstrument", stringValue);
        }

        public void BrowserMonitoringEnableAttributes(bool shouldEnableAttributes)
        {
            var stringValue = shouldEnableAttributes.ToString().ToLower(); //We don't seem to handle the uppercase parse
            CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(_configFilePath,
                new[] { "configuration", "browserMonitoring", "attributes" }, "enabled", stringValue);
        }

        public void BrowserMonitoringLoader(string loaderType)
        {
            CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(_configFilePath,
                new[] { "configuration", "browserMonitoring" }, "loader", loaderType);
        }

        public void PutForDataSend()
        {
            CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(_configFilePath, new[] { "configuration", "dataTransmission" },
                "putForDataSend", "true");
        }

        public void CompressedContentEncoding(string contentEncoding)
        {
            CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(_configFilePath, new[] { "configuration", "dataTransmission" },
                "compressedContentEncoding", contentEncoding);
        }

        public void SetReportingDatastoreInstance(bool isEnabled = true)
        {
            CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(_configFilePath,
                new[] { "configuration", "datastoreTracer", "instanceReporting" }, "enabled", "true");
        }

        public void SetReportingDatabaseName(bool isEnabled = true)
        {
            CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(_configFilePath,
                new[] { "configuration", "datastoreTracer", "databaseNameReporting" }, "enabled", "true");
        }

        public void ForceSqlTraces()
        {
            CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(_configFilePath, new[] { "configuration", "transactionTracer" },
                "explainThreshold", "1");
        }

        public void SetLogLevel(string level)
        {
            CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(_configFilePath, new[] { "configuration", "log" }, "level",
                level);
        }

        public void LogToConsole()
        {
            CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(_configFilePath, new[] { "configuration", "log" }, "console",
                "true");
        }

        public void SetLogDirectory(string directoryName)
        {
            CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(_configFilePath, new[] { "configuration", "log" }, "directory",
                directoryName);
        }

        public void SetLogFileName(string fileName)
        {
            CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(_configFilePath, new[] { "configuration", "log" }, "fileName",
                fileName);
        }


        public NewRelicConfigModifier EnableCat()
        {
            return SetCATEnabled(true);
        }

        public NewRelicConfigModifier SetCATEnabled(bool enabled)
        {
            CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(_configFilePath, new[] { "configuration" }, "crossApplicationTracingEnabled", enabled.ToString().ToLower());
            CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(_configFilePath, new[] { "configuration", "crossApplicationTracer" }, "enabled", enabled.ToString().ToLower());

            if (enabled)
            {
                SetOrDeleteDistributedTraceEnabled(null);
            }

            return this;
        }

        public void EnableSpanEvents(bool? value)
        {
            SetOrDeleteDistributedTraceEnabled(value);
            SetOrDeleteSpanEventsEnabled(value);
        }

        /// <summary>
        /// Sets or deletes the distributed trace enabled setting in the newrelic.config.
        /// </summary>
        /// <param name="enabled">If null, the setting will be deleted; otherwise, the setting will be set to the value of this parameter.</param>
        public void SetOrDeleteDistributedTraceEnabled(bool? enabled)
        {
            const string config = "configuration";
            const string distributedTracing = "distributedTracing";
            if (null == enabled)
            {
                CommonUtils.DeleteXmlNodeFromNewRelicConfig(_configFilePath, new[] { config }, distributedTracing);
            }
            else
            {
                CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(_configFilePath, new[] { config, distributedTracing },
                    "enabled", enabled.Value ? "true" : "false");
            }
        }

        /// <summary>
        /// Sets or deletes the excludeNewrelicHeader setting in the newrelic.config.
        /// </summary>
        /// <param name="enabled">If null, the setting will be deleted; otherwise, the setting will be set to the value of this parameter.</param>
        public void SetOrDeleteDistributedTraceExcludeNewRelicHeader(bool? exclude)
        {
            const string config = "configuration";
            const string distributedTracing = "distributedTracing";
            if (null == exclude)
            {
                CommonUtils.DeleteXmlNodeFromNewRelicConfig(_configFilePath, new[] { config }, distributedTracing);
            }
            else
            {
                CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(_configFilePath, new[] { config, distributedTracing },
                    "excludeNewrelicHeader", exclude.Value ? "true" : "false");
            }
        }

        public NewRelicConfigModifier SetAllowAllHeaders(bool? enabled)
        {
            const string config = "configuration";
            const string allowAllHeaders = "allowAllHeaders";
            if (null == enabled)
            {
                CommonUtils.DeleteXmlNodeFromNewRelicConfig(_configFilePath, new[] { config }, allowAllHeaders);
            }
            else
            {
                CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(_configFilePath, new[] { config, allowAllHeaders },
                    "enabled", enabled.Value ? "true" : "false");
            }

            return this;
        }

        public void SetOrDeleteSpanEventsEnabled(bool? enabled)
        {
            const string config = "configuration";
            const string spanEvents = "spanEvents";
            if (null == enabled)
            {
                CommonUtils.DeleteXmlNodeFromNewRelicConfig(_configFilePath, new[] { config }, spanEvents);
            }
            else
            {
                CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(_configFilePath, new[] { config, spanEvents },
                    "enabled", enabled.Value ? "true" : "false");
            }
        }

        public void SetCustomHostName(string customHostName)
        {
            CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(_configFilePath, new[] { "configuration", "processHost" },
                    "displayName", customHostName);
        }

        public NewRelicConfigModifier DisableApplicationLogging()
        {
            return EnableApplicationLogging(false);
        }

        public NewRelicConfigModifier EnableApplicationLogging(bool enable = true)
        {
            CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(_configFilePath, new[] { "configuration", "applicationLogging" }, "enabled", enable.ToString().ToLower());
            return this;
        }

        public NewRelicConfigModifier DisableLogMetrics()
        {
            return EnableLogMetrics(false);
        }

        public NewRelicConfigModifier EnableLogMetrics(bool enable = true)
        {
            CommonUtils.ModifyOrCreateXmlNodeInNewRelicConfig(_configFilePath, new[] { "configuration", "applicationLogging" }, "metrics", string.Empty);
            CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(_configFilePath, new[] { "configuration", "applicationLogging", "metrics" }, "enabled", enable.ToString().ToLower());
            return this;
        }

        public NewRelicConfigModifier DisableLogForwarding()
        {
            return EnableLogForwarding(false);
        }

        public NewRelicConfigModifier EnableLogForwarding(bool enable = true)
        {
            CommonUtils.ModifyOrCreateXmlNodeInNewRelicConfig(_configFilePath, new[] { "configuration", "applicationLogging" }, "forwarding", string.Empty);
            CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(_configFilePath, new[] { "configuration", "applicationLogging", "forwarding" }, "enabled", enable.ToString().ToLower());
            return this;
        }

        public NewRelicConfigModifier DisableLogDecoration()
        {
            return EnableLogDecoration(false);
        }

        public NewRelicConfigModifier EnableLogDecoration(bool enable = true)
        {
            CommonUtils.ModifyOrCreateXmlNodeInNewRelicConfig(_configFilePath, new[] { "configuration", "applicationLogging" }, "localDecorating", string.Empty);
            CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(_configFilePath, new[] { "configuration", "applicationLogging", "localDecorating" }, "enabled", enable.ToString().ToLower());
            return this;
        }

        public NewRelicConfigModifier SetLogForwardingMaxSamplesStored(int samples)
        {
            CommonUtils.ModifyOrCreateXmlNodeInNewRelicConfig(_configFilePath, new[] { "configuration", "applicationLogging" }, "forwarding", string.Empty);
            CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(_configFilePath, new[] { "configuration", "applicationLogging", "forwarding" }, "maxSamplesStored", samples.ToString());
            return this;
        }

        public NewRelicConfigModifier SetLogForwardingLogLevelDenyList(string logLevelDenyList)
        {
            CommonUtils.ModifyOrCreateXmlNodeInNewRelicConfig(_configFilePath, new[] { "configuration", "applicationLogging" }, "forwarding", string.Empty);
            CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(_configFilePath, new[] { "configuration", "applicationLogging", "forwarding" }, "logLevelDenyList", logLevelDenyList);
            return this;
        }

        public NewRelicConfigModifier SetCodeLevelMetricsEnabled(bool enabled = true)
        {
            CommonUtils.ModifyOrCreateXmlNodeInNewRelicConfig(_configFilePath, new[] { "configuration" }, "codeLevelMetrics", string.Empty);
            CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(_configFilePath, new[] { "configuration", "codeLevelMetrics" }, "enabled", enabled.ToString().ToLower());
            return this;
        }

        public NewRelicConfigModifier EnableContextData(bool enabled = true)
        {
            CommonUtils.ModifyOrCreateXmlNodeInNewRelicConfig(_configFilePath, new[] { "configuration", "applicationLogging", "forwarding" }, "contextData", string.Empty);
            CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(_configFilePath, new[] { "configuration", "applicationLogging", "forwarding", "contextData" }, "enabled", enabled.ToString().ToLower());

            return this;
        }

        public NewRelicConfigModifier SetSendDataOnExit(bool enabled = true)
        {
            CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(_configFilePath, new[] { "configuration", "service" }, "sendDataOnExit", enabled.ToString().ToLower());
            return this;
        }

        public NewRelicConfigModifier EnableAgentTiming(bool enable = true)
        {
            CommonUtils.ModifyOrCreateXmlNodeInNewRelicConfig(_configFilePath, new[] { "configuration" }, "diagnostics", string.Empty);
            CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(_configFilePath, new[] { "configuration", "diagnostics"}, "captureAgentTiming", enable.ToString().ToLower());
            return this;
        }

        public NewRelicConfigModifier ConfigureFasterMetricsHarvestCycle(int seconds)
        {
            CommonUtils.SetConfigAppSetting(_configFilePath, "OverrideMetricsHarvestCycle", seconds.ToString(), "urn:newrelic-config");
            return this;
        }

        public NewRelicConfigModifier ConfigureFasterTransactionTracesHarvestCycle(int seconds)
        {
            CommonUtils.SetConfigAppSetting(_configFilePath, "OverrideTransactionTracesHarvestCycle", seconds.ToString(), "urn:newrelic-config");
            return this;
        }

        public NewRelicConfigModifier ConfigureFasterSpanEventsHarvestCycle(int seconds)
        {
            CommonUtils.SetConfigAppSetting(_configFilePath, "OverrideSpanEventsHarvestCycle", seconds.ToString(), "urn:newrelic-config");
            return this;
        }

        public NewRelicConfigModifier ConfigureFasterErrorTracesHarvestCycle(int seconds)
        {
            CommonUtils.SetConfigAppSetting(_configFilePath, "OverrideErrorTracesHarvestCycle", seconds.ToString(), "urn:newrelic-config");
            return this;
        }

        public NewRelicConfigModifier ConfigureFasterGetAgentCommandsCycle(int seconds)
        {
            CommonUtils.SetConfigAppSetting(_configFilePath, "OverrideGetAgentCommandsCycle", seconds.ToString(), "urn:newrelic-config");
            return this;
        }

        public NewRelicConfigModifier ConfigureFasterSqlTracesHarvestCycle(int seconds)
        {
            CommonUtils.SetConfigAppSetting(_configFilePath, "OverrideSqlTracesHarvestCycle", seconds.ToString(), "urn:newrelic-config");
            return this;
        }

        public NewRelicConfigModifier ConfigureFasterUpdateLoadedModulesCycle(int seconds)
        {
            CommonUtils.SetConfigAppSetting(_configFilePath, "OverrideUpdateLoadedModulesCycle", seconds.ToString(), "urn:newrelic-config");
            return this;
        }

        public NewRelicConfigModifier EnableAspNetCore6PlusBrowserInjection(bool enableBrowserInjection)
        {
            CommonUtils.ModifyOrCreateXmlNodeInNewRelicConfig(_configFilePath, new[] { "configuration" }, "appSettings", string.Empty);
            CommonUtils.ModifyOrCreateXmlNodeInNewRelicConfig(_configFilePath, new[] { "configuration", "appSettings" }, "add", string.Empty);
            CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(_configFilePath, new[] { "configuration", "appSettings", "add"}, "key", "EnableAspNetCore6PlusBrowserInjection");
            CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(_configFilePath, new[] { "configuration", "appSettings", "add"}, "value", $"{enableBrowserInjection}");
            return this;
        }

        public NewRelicConfigModifier DisableEventListenerSamplers()
        {
            CommonUtils.SetConfigAppSetting(_configFilePath, "NewRelic.EventListenerSamplersEnabled", "false", "urn:newrelic-config");
            return this;
        }

        public void EnableAuditLog(bool enableAuditLog)
        {
            CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(_configFilePath, new[] { "configuration", "log" }, "auditLog",
                enableAuditLog.ToString().ToLower());
        }

        public NewRelicConfigModifier AddIgnoredInstrumentationAssembly(string assemblyName)
        {
            return AddIgnoredInstrumentationAssemblyAndClass(assemblyName, null);
        }

        public NewRelicConfigModifier AddIgnoredInstrumentationAssemblyAndClass(string assemblyName, string className)
        {
            CommonUtils.AddIgnoredInstrumentation(_configFilePath, assemblyName, className);
            return this;
        }

        public NewRelicConfigModifier RemoveIgnoredInstrumentationAssembly(string assemblyName)
        {
            return RemoveIgnoredInstrumentationAssemblyAndClass(assemblyName, null);
        }

        public NewRelicConfigModifier RemoveIgnoredInstrumentationAssemblyAndClass(string assemblyName, string className)
        {
            CommonUtils.RemoveIgnoredInstrumentation(_configFilePath, assemblyName, className);
            return this;
        }

        public NewRelicConfigModifier EnableAiMonitoring(bool enabled = true)
        {
            CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(_configFilePath, new[] { "configuration", "aiMonitoring" }, "enabled", enabled.ToString().ToLower());
            return this;
        }

        public void SetCompleteTransactionsOnThread(bool enable)
        {
            CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(_configFilePath, new[] { "configuration", "service" },
                "completeTransactionsOnThread", enable ? "true" : "false");
        }

        public void SetSyncStartup(bool enable)
        {
            CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(_configFilePath, new[] { "configuration", "service" },
                "syncStartup", enable ? "true" : "false");
        }

        public void SetDebugStartupDelaySeconds(int delaySeconds)
        {
            CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(_configFilePath, new[] { "configuration", },
                "debugStartupDelaySeconds", delaySeconds.ToString());
        }
    }
}
