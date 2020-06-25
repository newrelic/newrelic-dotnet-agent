/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
namespace NewRelic.Agent.Core.Configuration
{
    public class BoolConfigurationItem
    {
        public bool Value { get; }
        public string Source { get; }

        public BoolConfigurationItem(bool value, string source)
        {
            Value = value;
            Source = source;
        }
    }
}

