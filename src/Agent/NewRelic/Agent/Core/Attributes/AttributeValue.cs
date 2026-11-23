// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Diagnostics;

namespace NewRelic.Agent.Core.Attributes;

public interface IAttributeValue
{
    AttributeDefinition AttributeDefinition { get; }

    string Name { get; }

    AttributeDestinations AttributeDestinations { get; }

    AttributeKey Key { get; }

    object Value { get; set; }

    Lazy<object> LazyValue { get; set; }

    void MakeImmutable();
}

[DebuggerDisplay("{_value ?? _lazyValue}")]
public class AttributeValue : IAttributeValue
{
    private readonly string _name;
    private readonly AttributeDestinations _destinations;

    public AttributeValue(AttributeDefinition attribDef)
    {
        AttributeDefinition = attribDef;
    }

    public AttributeValue(AttributeDefinition attribDef, string name, AttributeDestinations destinations) : this(attribDef)
    {
        _name = name;
        _destinations = destinations;
    }

    public AttributeDefinition AttributeDefinition { get; private set; }

    public string Name => _name ?? AttributeDefinition.Name;

    public AttributeDestinations AttributeDestinations => _name != null ? _destinations : AttributeDefinition.AttributeDestinations;

    public AttributeKey Key => new AttributeKey(AttributeDefinition.Guid, _name);

    private object _value;
    public object Value
    {
        get => _value;
        set
        {
            if (IsImmutable) return;
            _value = value;
            _lazyValue = null;
        }
    }

    private Lazy<object> _lazyValue;
    public Lazy<object> LazyValue
    {
        get => _lazyValue;
        set
        {
            if (IsImmutable) return;
            _lazyValue = value;
            _value = null;
        }
    }

    public bool IsImmutable { get; private set; }

    public void MakeImmutable()
    {
        if (IsImmutable)
        {
            return;
        }

        if (LazyValue != null && Value == null)
        {
            Value = LazyValue.Value;
        }

        IsImmutable = true;
    }
}