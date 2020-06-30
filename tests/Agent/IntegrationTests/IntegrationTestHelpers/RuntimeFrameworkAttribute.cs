/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/

using System;
using System.Collections.Generic;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace NewRelic.Agent.IntegrationTestHelpers
{
    [TraitDiscoverer("NewRelic.Agent.IntegrationTestHelpers.RuntimeFrameworkDiscoverer", "NewRelic.Agent.IntegrationTestHelpers")]
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public abstract class RuntimeFrameworkAttribute : Attribute, ITraitAttribute
    {
        public abstract string RuntimeFramework { get; }
    }

    public class RuntimeFrameworkDiscoverer : ITraitDiscoverer
    {
        public IEnumerable<KeyValuePair<string, string>> GetTraits(IAttributeInfo traitAttribute)
        {
            yield return new KeyValuePair<string, string>("RuntimeFramework", traitAttribute.GetNamedArgument<string>("RuntimeFramework"));
        }
    }

    public class NetCoreTestAttribute : RuntimeFrameworkAttribute
    {
        public override string RuntimeFramework => "NetCore";
    }

    public class NetFrameworkTestAttribute : RuntimeFrameworkAttribute
    {
        public override string RuntimeFramework => "NetFramework";
    }
}
