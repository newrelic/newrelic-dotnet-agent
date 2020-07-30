/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
using System;
using System.Collections.Generic;

namespace NewRelic.Agent.Core.Api
{
    public interface IAgentApi
    {
        void RecordCustomEvent(string eventType, IEnumerable<KeyValuePair<string, object>> attributes);
        void RecordMetric(string name, float value);
        void RecordResponseTimeMetric(string name, long millis);
        void IncrementCounter(string name);
        void NoticeError(Exception exception, IDictionary<string, string> customAttributes);
        void NoticeError(Exception exception);
        void NoticeError(string message, IDictionary<string, string> customAttributes);
        void AddCustomParameter(string key, IConvertible value);
        void AddCustomParameter(string key, string value);
        void SetTransactionName(string category, string name);
        void SetTransactionUri(Uri uri);
        void SetUserParameters(string userName, string accountName, string productName);
        void IgnoreTransaction();
        void IgnoreApdex();
        string GetBrowserTimingHeader();
        string GetBrowserTimingFooter();
        void DisableBrowserMonitoring(bool overrideManual = false);
        void StartAgent();
        void SetApplicationName(string applicationName, string applicationName2 = null, string applicationName3 = null);
        IEnumerable<KeyValuePair<string, string>> GetRequestMetadata();
        IEnumerable<KeyValuePair<string, string>> GetResponseMetadata();
    }
}
