// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.Collections.Generic;

namespace NewRelic.Agent.MultiverseScanner.Models
{
    public class MethodModel
    {
        public string Name { get; }

        public string AccessLevel { get; }

        public List<string> ParameterSets { get; }

        public MethodModel(string name)
        {
            ParameterSets = new List<string>();
            Name = name;
        }
    }
}
