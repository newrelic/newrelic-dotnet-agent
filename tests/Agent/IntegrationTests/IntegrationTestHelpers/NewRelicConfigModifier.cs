using System;
using JetBrains.Annotations;

namespace NewRelic.Agent.IntegrationTestHelpers
{
    public class NewRelicConfigModifier
    {
        private readonly String _configFilePath;

        public NewRelicConfigModifier([NotNull] String configFilePath)
        {
            _configFilePath = configFilePath;
        }

        public void ForceTransactionTraces()
        {
            CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(_configFilePath, new[] { "configuration", "transactionTracer" }, "transactionThreshold", "1");
        }

        public void AutoInstrumentBrowserMonitoring(Boolean shouldAutoInstrument)
        {
            var stringValue = shouldAutoInstrument.ToString().ToLower(); //We don't seem to handle the uppercase parse
            CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(_configFilePath, new[] { "configuration", "browserMonitoring" }, "autoInstrument", stringValue);
        }

        public void BrowserMonitoringEnableAttributes(Boolean shouldEnableAttributes)
        {
            var stringValue = shouldEnableAttributes.ToString().ToLower(); //We don't seem to handle the uppercase parse
            CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(_configFilePath, new[] { "configuration", "browserMonitoring", "attributes" }, "enabled", stringValue);
        }

        public void PutForDataSend()
        {
            CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(_configFilePath, new[] { "configuration", "dataTransmission" }, "putForDataSend", "true");
        }

        public void CompressedContentEncoding(String contentEncoding)
        {
            CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(_configFilePath, new[] { "configuration", "dataTransmission" }, "compressedContentEncoding", contentEncoding);
        }

        public void SetReportingDatastoreInstance(Boolean isEnabled = true)
        {
            CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(_configFilePath, new[] { "configuration", "datastoreTracer", "instanceReporting" }, "enabled", "true");
        }

        public void SetReportingDatabaseName(Boolean isEnabled = true)
        {
            CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(_configFilePath, new[] { "configuration", "datastoreTracer", "databaseNameReporting" }, "enabled", "true");
        }

        public void ForceSqlTraces()
        {
            CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(_configFilePath, new[] { "configuration", "transactionTracer" }, "explainThreshold", "1");
        }

        public void SetLogLevel(string level)
        {
            CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(_configFilePath, new[] { "configuration", "log" }, "level", level);
        }
    }
}
