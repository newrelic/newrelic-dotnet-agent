using System;
using System.Collections.Generic;

namespace NewRelic.Agent.Core.Api
{
	public interface IAgentApi
	{
		void RecordCustomEvent(String eventType, IEnumerable<KeyValuePair<String, Object>> attributes);
		void RecordMetric(String name, Single value);
		void RecordResponseTimeMetric(String name, Int64 millis);
		void IncrementCounter(String name);
		void NoticeError(Exception exception, IDictionary<String, String> customAttributes);
		void NoticeError(Exception exception);
		void NoticeError(String message, IDictionary<String, String> customAttributes);
		void AddCustomParameter(String key, IConvertible value);
		void AddCustomParameter(String key, String value);
		void SetTransactionName(String category, String name);
		void SetTransactionUri(Uri uri);
		void SetUserParameters(String userName, String accountName, String productName);
		void IgnoreTransaction();
		void IgnoreApdex();
		String GetBrowserTimingHeader();
		String GetBrowserTimingFooter();
		void DisableBrowserMonitoring(Boolean overrideManual = false);
		void StartAgent();
		void SetApplicationName(String applicationName, String applicationName2 = null, String applicationName3 = null);
		IEnumerable<KeyValuePair<String, String>> GetRequestMetadata();
		IEnumerable<KeyValuePair<String, String>> GetResponseMetadata();
		void AcceptDistributedTracePayload(IEnumerable<KeyValuePair<String, String>> distributedTracePayload);
		IEnumerable<KeyValuePair<String, String>> CreateDistributedTracePayload();
	}
}