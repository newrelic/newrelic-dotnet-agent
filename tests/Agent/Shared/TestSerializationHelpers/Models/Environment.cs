// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace NewRelic.Agent.Tests.TestSerializationHelpers.Models
{
    public class Environment : List<object[]>
    {
        public object[] GetProperty(string Name)
        {
            return this.FirstOrDefault(env => string.Equals(Name, env[0].ToString(), StringComparison.OrdinalIgnoreCase));
        }

        public string GetPropertyString(string Name)
        {
            var property = GetProperty(Name);

            if (property == null || property.Length <= 1)
                return null;

            return property[1]?.ToString();
        }

        public List<string> GetPluginList()
        {
            var pluginListStructure = GetProperty("Plugin List");

            if (pluginListStructure == null)
            {
                return new List<string>(0);
            }

            var plugins = ((JArray)pluginListStructure[1]).Values<string>().ToList();

            return plugins;
        }
    }
}
