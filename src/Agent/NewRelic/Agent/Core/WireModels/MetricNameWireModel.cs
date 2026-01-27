// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Core.JsonConverters;
using Newtonsoft.Json;

namespace NewRelic.Agent.Core.WireModels;

[JsonConverter(typeof(MetricNameWireModelJsonConverter))]
public class MetricNameWireModel
{
    private const string PropertyName = "name";
    private const string PropertyScope = "scope";

    // property name: "name"
    public readonly string Name;

    // property name: "scope"
    public readonly string Scope;

    // We cache the hash code for MetricNameWireModel because it is guaranteed that we will need it at least once
    private readonly int _hashCode;
    private static int HashCodeCombiner(int h1, int h2)
    {
        var rol5 = ((uint)h1 << 5) | ((uint)h1 >> 27);
        return ((int)rol5 + h1) ^ h2;
    }

    public MetricNameWireModel(string name, string scope)
    {
        Name = name;
        Scope = scope;

        //no heap allocation to compute hash code
        _hashCode = HashCodeCombiner(Name.GetHashCode(), Scope?.GetHashCode() ?? 0);
    }

    public override bool Equals(object obj)
    {
        if (ReferenceEquals(this, obj))
            return true;

        return obj is MetricNameWireModel other && Name == other.Name && Scope == other.Scope;
    }

    public override int GetHashCode()
    {
        return _hashCode;
    }

    public override string ToString()
    {
        return $"{Name} ({Scope})";
    }
}