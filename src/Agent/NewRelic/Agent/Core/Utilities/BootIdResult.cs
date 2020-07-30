/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
namespace NewRelic.Agent.Core.Utilization
{
    public class BootIdResult
    {
        public string BootId { get; }
        public bool IsValid { get; }

        public BootIdResult(string bootId, bool isValid)
        {
            BootId = bootId;
            IsValid = isValid;
        }

    }
}
