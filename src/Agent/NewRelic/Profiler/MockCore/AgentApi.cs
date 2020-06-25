/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
using System;
using System.Collections.Generic;
using System.Threading;

namespace NewRelic.Agent.Core
{
    class AgentApi
    {
        public static void RecordMetric(String name, Single value)
        {
            var delegateDataSlot = Thread.GetNamedDataSlot("NewRelic_Test_Api_RecordMetric_Delegate");
            var getTracerDelegate = (Delegate)Thread.GetData(delegateDataSlot);
            getTracerDelegate.DynamicInvoke(new object[] { name, value });
        }

        public static void RecordResponseTimeMetric(String name, Int64 millis)
        {
            var delegateDataSlot = Thread.GetNamedDataSlot("NewRelic_Test_Api_RecordResponseTimeMetric_Delegate");
            var getTracerDelegate = (Delegate)Thread.GetData(delegateDataSlot);
            getTracerDelegate.DynamicInvoke(new object[] { name, millis });
        }

        public static void IncrementCounter(String name)
        {
            var delegateDataSlot = Thread.GetNamedDataSlot("NewRelic_Test_Api_IncrementCounter_Delegate");
            var getTracerDelegate = (Delegate)Thread.GetData(delegateDataSlot);
            getTracerDelegate.DynamicInvoke(new object[] { name });
        }

        public static void NoticeError(Exception exception, IDictionary<String, String> parameters)
        {
            var delegateDataSlot = Thread.GetNamedDataSlot("NewRelic_Test_Api_NoticeError1_Delegate");
            var getTracerDelegate = (Delegate)Thread.GetData(delegateDataSlot);
            getTracerDelegate.DynamicInvoke(new object[] { exception, parameters });
        }

        public static void NoticeError(Exception exception)
        {
            var delegateDataSlot = Thread.GetNamedDataSlot("NewRelic_Test_Api_NoticeError2_Delegate");
            var getTracerDelegate = (Delegate)Thread.GetData(delegateDataSlot);
            getTracerDelegate.DynamicInvoke(new object[] { exception });
        }

        public static void NoticeError(String message, IDictionary<String, String> parameters)
        {
            var delegateDataSlot = Thread.GetNamedDataSlot("NewRelic_Test_Api_NoticeError3_Delegate");
            var getTracerDelegate = (Delegate)Thread.GetData(delegateDataSlot);
            getTracerDelegate.DynamicInvoke(new object[] { message, parameters });
        }

        public static void AddCustomParameter(String key, IConvertible value)
        {
            var delegateDataSlot = Thread.GetNamedDataSlot("NewRelic_Test_Api_AddCustomParameter1_Delegate");
            var getTracerDelegate = (Delegate)Thread.GetData(delegateDataSlot);
            getTracerDelegate.DynamicInvoke(new object[] { key, value });
        }

        public static void AddCustomParameter(String key, String value)
        {
            var delegateDataSlot = Thread.GetNamedDataSlot("NewRelic_Test_Api_AddCustomParameter2_Delegate");
            var getTracerDelegate = (Delegate)Thread.GetData(delegateDataSlot);
            getTracerDelegate.DynamicInvoke(new object[] { key, value });
        }

        public static void SetTransactionName(String category, String name)
        {
            var delegateDataSlot = Thread.GetNamedDataSlot("NewRelic_Test_Api_SetTransactionName_Delegate");
            var getTracerDelegate = (Delegate)Thread.GetData(delegateDataSlot);
            getTracerDelegate.DynamicInvoke(new object[] { category, name });
        }

        public static void SetUserParameters(String userName, String accountName, String productName)
        {
            var delegateDataSlot = Thread.GetNamedDataSlot("NewRelic_Test_Api_SetUserParameters_Delegate");
            var getTracerDelegate = (Delegate)Thread.GetData(delegateDataSlot);
            getTracerDelegate.DynamicInvoke(new object[] { userName, accountName, productName });
        }

        public static void IgnoreTransaction()
        {
            var delegateDataSlot = Thread.GetNamedDataSlot("NewRelic_Test_Api_IgnoreTransaction_Delegate");
            var getTracerDelegate = (Delegate)Thread.GetData(delegateDataSlot);
            getTracerDelegate.DynamicInvoke(new object[] { });
        }

        public static void IgnoreApdex()
        {
            var delegateDataSlot = Thread.GetNamedDataSlot("NewRelic_Test_Api_IgnoreApdex_Delegate");
            var getTracerDelegate = (Delegate)Thread.GetData(delegateDataSlot);
            getTracerDelegate.DynamicInvoke(new object[] { });
        }

        public static String GetBrowserTimingHeader()
        {
            var delegateDataSlot = Thread.GetNamedDataSlot("NewRelic_Test_Api_GetBrowserTimingHeader_Delegate");
            var getTracerDelegate = (Delegate)Thread.GetData(delegateDataSlot);
            var result = getTracerDelegate.DynamicInvoke(new object[] { });
            return result as String;
        }

        public static String GetBrowserTimingFooter()
        {
            var delegateDataSlot = Thread.GetNamedDataSlot("NewRelic_Test_Api_GetBrowserTimingFooter_Delegate");
            var getTracerDelegate = (Delegate)Thread.GetData(delegateDataSlot);
            var result = getTracerDelegate.DynamicInvoke(new object[] { });
            return result as String;
        }

        public static void DisableBrowserMonitoring(bool overrideManual = false)
        {
            var delegateDataSlot = Thread.GetNamedDataSlot("NewRelic_Test_Api_DisableBrowserMonitoring_Delegate");
            var getTracerDelegate = (Delegate)Thread.GetData(delegateDataSlot);
            getTracerDelegate.DynamicInvoke(new object[] { overrideManual });
        }
    }
}
