// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


namespace NewRelic.Agent.Tests.TestSerializationHelpers.Models
{
    public class PythonTestFailureEntryModel
    {
        public string TestName;
        public string StackTrace;
        public string AssertionError;

        public PythonTestFailureEntryModel(string testName, string stackTrace, string assertionError)
        {
            TestName = testName;
            StackTrace = stackTrace;
            AssertionError = assertionError;
        }

        public override string ToString() => $"FAIL: {TestName}\r\n{StackTrace}\r\nAssertionError: {AssertionError}";
    }
}
