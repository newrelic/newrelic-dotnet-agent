// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


namespace NewRelic.Agent.IntegrationTestHelpers
{
    public interface ITestLogger
    {
        void WriteLine(string message);
        void WriteLine(string format, params object[] args);
        void WriteFormattedOutput(string formattedOutput);
    }
}
