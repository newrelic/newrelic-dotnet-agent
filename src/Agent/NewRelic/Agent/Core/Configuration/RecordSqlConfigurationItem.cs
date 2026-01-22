// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NewRelic.Agent.Core.Config;

namespace NewRelic.Agent.Core.Configuration;

public class RecordSqlConfigurationItem
{
    public string Value { get; private set; }
    public string Source { get; private set; }

    public RecordSqlConfigurationItem(string value, string source)
    {
        Value = value;
        Source = source;
    }

    public RecordSqlConfigurationItem ApplyIfMoreRestrictive(RecordSqlConfigurationItem newConfiguration)
    {
        if (newConfiguration == null)
        {
            return this;
        }

        return ApplyIfMoreRestrictive(newConfiguration.Value, newConfiguration.Source);
    }

    public RecordSqlConfigurationItem ApplyIfMoreRestrictive(string newValue, string newSource)
    {
        if (DefaultConfiguration.OffStringValue.Equals(Value, StringComparison.InvariantCultureIgnoreCase))
        {
            return this;
        }

        if (DefaultConfiguration.OffStringValue.Equals(newValue, StringComparison.InvariantCultureIgnoreCase))
        {
            Value = DefaultConfiguration.OffStringValue;
            Source = newSource;
        }
        else if (DefaultConfiguration.ObfuscatedStringValue.Equals(newValue, StringComparison.InvariantCultureIgnoreCase)
                 && DefaultConfiguration.RawStringValue.Equals(Value, StringComparison.InvariantCultureIgnoreCase))
        {
            Value = DefaultConfiguration.ObfuscatedStringValue;
            Source = newSource;
        }

        return this;
    }

    public RecordSqlConfigurationItem ApplyIfMoreRestrictive(configurationTransactionTracerRecordSql newValue, string newSource)
    {
        if (Value == DefaultConfiguration.OffStringValue)
        {
            return this;
        }

        if (newValue == configurationTransactionTracerRecordSql.off)
        {
            Value = DefaultConfiguration.OffStringValue;
            Source = newSource;
        }
        else if (newValue == configurationTransactionTracerRecordSql.obfuscated
                 && Value == DefaultConfiguration.RawStringValue)
        {
            Value = DefaultConfiguration.ObfuscatedStringValue;
            Source = newSource;
        }

        return this;
    }
}