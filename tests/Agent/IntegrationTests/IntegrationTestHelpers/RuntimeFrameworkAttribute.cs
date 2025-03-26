// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;
using Xunit;
using Xunit.Sdk;
using Xunit.v3;

namespace NewRelic.Agent.IntegrationTestHelpers
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public abstract class RuntimeFrameworkAttribute : Attribute, ITraitAttribute
    {
        public abstract string RuntimeFramework { get; }

        public IReadOnlyCollection<KeyValuePair<string, string>> GetTraits() => [new("RuntimeFramework", RuntimeFramework)];
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
