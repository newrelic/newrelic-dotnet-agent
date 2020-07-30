/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
using System;

namespace NewRelic.Agent.IntegrationTests.Applications.WcfAppIisHosted
{
    public class MyService : IMyService
    {
        public string GetData(int value)
        {
            return string.Format("You entered: {0}", value);
        }

        public string IgnoredTransaction(string input)
        {
            NewRelic.Api.Agent.NewRelic.IgnoreTransaction();
            return input;
        }
        public void ThrowException()
        {
            throw new Exception("ExceptionMessage");
        }
    }
}
