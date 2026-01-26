// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;

namespace NewRelic.Agent.Core.Config;

/// <summary>
/// Configuration objects use this attribute on properties to map them to settings returned from the server.
/// This attribute is also used when reporting the agent's settings back to the server.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class ConfigurationAttribute : Attribute
{
    private readonly string key;
    private readonly bool convertToString;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="key"></param>
    /// <param name="convertToString">If true, the ConfigurationParser.GetSettings method will calll ToString() on the property
    /// before reporting it to the New Relic service.</param>
    public ConfigurationAttribute(string key, bool convertToString = false)
    {
        this.key = key;
        this.convertToString = convertToString;
    }

    /// <summary>
    /// The server key to which the annotated property should be mapped.
    /// </summary>
    public string Key
    {
        get { return key; }
    }

    /// <summary>
    /// If true, the ConfigurationParser.GetSettings method will calll ToString() on the property
    /// before reporting it to the New Relic service.
    /// </summary>
    public bool ConvertToString
    {
        get { return convertToString; }
    }
}
