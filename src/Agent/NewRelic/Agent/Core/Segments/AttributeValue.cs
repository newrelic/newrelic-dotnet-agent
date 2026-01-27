// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NewRelic.Agent.Core.Attributes;

namespace NewRelic.Agent.Core.Segments;

//This is the Infinite Tracing (gRPC) implementation of attribute value
public partial class AttributeValue : IAttributeValue
{
    private readonly AttributeDefinition _attributeDefinition;
    public AttributeDefinition AttributeDefinition => _attributeDefinition;

    public AttributeValue(AttributeDefinition attributeDefinition)
    {
        _attributeDefinition = attributeDefinition;
    }

    public AttributeValue(IAttributeValue attribValue) : this(attribValue.AttributeDefinition)
    {
        if (attribValue.Value != null)
        {
            SetValue(attribValue.Value);
        }
        else if (attribValue.LazyValue != null)
        {
            SetValue(attribValue.LazyValue);
        }
    }

    public bool IsImmutable { get; private set; }

    private Lazy<object> _lazyValue;
    public Lazy<object> LazyValue
    {
        get => _lazyValue;
        set => SetValue(value);
    }

    public object Value
    {
        get
        {
            switch (ValueCase)
            {
                case ValueOneofCase.StringValue:
                    return StringValue;
                case ValueOneofCase.BoolValue:
                    return BoolValue;
                case ValueOneofCase.IntValue:
                    return IntValue;
                case ValueOneofCase.DoubleValue:
                    return DoubleValue;
                default:
                    return null;
            }
        }

        set => SetValue(value);

    }

    public void MakeImmutable()
    {
        if (IsImmutable)
        {
            return;
        }

        if (Value == null && LazyValue != null)
        {
            SetValue(LazyValue.Value);
        }

        IsImmutable = true;
    }

    private void SetValue(Lazy<object> lazyValue)
    {
        if (IsImmutable || lazyValue == null)
        {
            return;
        }

        _lazyValue = lazyValue;
    }

    private void SetValue(object value)
    {
        if (IsImmutable || value == null)
        {
            return;
        }

        if (value is string)
        {
            StringValue = (string)value;
            return;
        }

        if (value is double)
        {
            DoubleValue = (double)value;
            return;
        }

        if (value is bool)
        {
            BoolValue = (bool)value;
            return;
        }

        if (value is long)
        {
            IntValue = (long)value;
            return;
        }

        if (value is TimeSpan)
        {
            DoubleValue = ((TimeSpan)value).TotalSeconds;
            return;
        }

        if (value is DateTimeOffset)
        {
            StringValue = ((DateTimeOffset)value).ToString("o");
            return;
        }

        switch (Type.GetTypeCode(value.GetType()))
        {
            case TypeCode.SByte:
            case TypeCode.Byte:
            case TypeCode.Int16:
            case TypeCode.UInt16:
            case TypeCode.Int32:
            case TypeCode.UInt32:
            case TypeCode.UInt64:
                IntValue = Convert.ToInt64(value);
                break;

            case TypeCode.Single:
            case TypeCode.Decimal:
                DoubleValue = Convert.ToDouble(value);
                break;

            case TypeCode.Object:
            case TypeCode.Char:
                StringValue = value.ToString();
                break;

            case TypeCode.DateTime:
                StringValue = ((DateTime)value).ToString("o");
                break;
        }
    }
}