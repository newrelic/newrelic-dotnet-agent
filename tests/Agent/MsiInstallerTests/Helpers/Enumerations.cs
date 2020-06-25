/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
namespace FunctionalTests.Helpers
{
    public static class Enumerations
    {
        public enum EnvironmentSetting { Local, Remote, Developer };
        public enum InstallFeatures { StartMenuShortcuts, InstrumentAllNETFramework, NETFrameworkSupport, ASPNETTools, NETCoreSupport };
    }
}
