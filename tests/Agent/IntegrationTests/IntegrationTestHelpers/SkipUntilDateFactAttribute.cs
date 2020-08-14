// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using Xunit;

namespace NewRelic.Agent.IntegrationTestHelpers
{
    public sealed class SkipUntilDateFactAttribute : FactAttribute
    {
        public SkipUntilDateFactAttribute(string date, string reason)
        {
            if (DateTime.TryParse(date, out DateTime dateTime))
            {
                if (DateTime.Now < dateTime)
                {
                    Skip = $"Skipping test until {dateTime}, reason = {reason}";
                }
            }
        }
    }
}
