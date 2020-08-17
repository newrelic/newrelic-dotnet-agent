// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


namespace NewRelic.Agent.IntegrationTests.Shared
{
    public class ApplicationConfiguration
    {
        public static string WinDatabaseServerIp = "172.17.0.13";
        public static string Db2ConnectionString = $"Server={WinDatabaseServerIp};Database=SAMPLE;UserID=jenkins;Password=**REDACTED**";
    }
}
