// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.SystemInterfaces
{
    public class Environment : IEnvironment
    {
        public string GetEnvironmentVariable(string variable)
        {
            return System.Environment.GetEnvironmentVariable(variable);
        }
    }
}
