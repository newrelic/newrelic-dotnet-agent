/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace NewRelic.Agent.IntegrationTestHelpers.Models
{
    public class Environment : List<object[]>
    {
        public List<string> GetPluginList()
        {
            var pluginListStructure = this
                .FirstOrDefault(env => string.Equals("Plugin List", env[0].ToString(), StringComparison.InvariantCultureIgnoreCase));

            if (pluginListStructure == null)
            {
                return new List<string>(0);
            }

            var plugins = ((JArray)pluginListStructure[1]).Values<string>().ToList();

            return plugins;
        }
    }
}
