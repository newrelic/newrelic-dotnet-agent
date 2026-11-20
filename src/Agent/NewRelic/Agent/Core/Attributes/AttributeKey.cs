// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;

namespace NewRelic.Agent.Core.Attributes;

/// <summary>
/// Identity of an attribute value in a collection: the definition Guid, plus the full name for a dynamic attribute.
/// Name is null for a static attribute. Names compare ordinally.
/// </summary>
public readonly struct AttributeKey : IEquatable<AttributeKey>
{
    public Guid DefinitionGuid { get; }

    public string Name { get; }

    public AttributeKey(Guid definitionGuid, string name)
    {
        DefinitionGuid = definitionGuid;
        Name = name;
    }

    public bool Equals(AttributeKey other)
    {
        return DefinitionGuid == other.DefinitionGuid && string.Equals(Name, other.Name, StringComparison.Ordinal);
    }

    public override bool Equals(object obj)
    {
        return obj is AttributeKey other && Equals(other);
    }

    public override int GetHashCode()
    {
        return Name == null
            ? DefinitionGuid.GetHashCode()
            : unchecked(DefinitionGuid.GetHashCode() * 397 ^ StringComparer.Ordinal.GetHashCode(Name));
    }
}
