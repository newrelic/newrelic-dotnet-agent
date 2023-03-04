// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;

namespace NewRelic.Agent.Core.Errors
{
    public class ErrorData
    {
        public string ErrorMessage { get; }
        public string ErrorTypeName { get; }
        public string StackTrace { get; }
        public DateTime NoticedAt { get; }
        public string Path { get; set; }
        public ReadOnlyDictionary<string, object> CustomAttributes { get; }
        public bool IsExpected { get; }

        public const string StripExceptionMessagesMessage = "Message removed by New Relic based on your currently enabled security settings.";

        public ErrorData(string errorMessage, string errorTypeName, string stackTrace, DateTime noticedAt, ReadOnlyDictionary<string, object> customAttributes, bool isExpected)
        {
            NoticedAt = noticedAt;
            StackTrace = stackTrace;
            ErrorMessage = errorMessage;
            ErrorTypeName = errorTypeName;
            Path = null;
            CustomAttributes = customAttributes;
            IsExpected = isExpected;
        }
    }
}
