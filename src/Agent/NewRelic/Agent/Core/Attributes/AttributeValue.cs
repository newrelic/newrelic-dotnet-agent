// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Diagnostics;

namespace NewRelic.Agent.Core.Attributes;

public interface IAttributeValue
{
    AttributeDefinition AttributeDefinition { get; }

    object Value { get; set; }

    Lazy<object> LazyValue { get; set; }

    void MakeImmutable();
}

[DebuggerDisplay("{_value ?? _lazyValue}")]
public class AttributeValue : IAttributeValue
{
    public AttributeValue(AttributeDefinition attribDef)
    {
        AttributeDefinition = attribDef;
    }

    public AttributeDefinition AttributeDefinition { get; private set; }

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