// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


namespace NewRelic.Agent.Tests.TestSerializationHelpers.Models
{
    public class PythonTestSummaryEntryModel
    {
        public string Name;
        public string TestClass;
        public string Result;

        public PythonTestSummaryEntryModel(string name, string testClass, string result)
        {
            Name = name;
            TestClass = testClass;
            Result = result;
        }

        public override string ToString() => $"{Name} ({TestClass}) ... {Result}";
    }
}
