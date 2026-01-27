// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Linq;
using NewRelic.Agent.Core.DistributedTracing;
using Newtonsoft.Json.Linq;

namespace NewRelic.Agent.Core.JsonConverters;

/// <summary>
/// Represents a constraint that is applied during de-serialization of an arbitrary type <typeparamref name="T"/> into JSON.
/// </summary>
/// <typeparam name="T">An arbitrary class or struct to be deserialized</typeparam>
public class ValidationConstraint<T>
{
    /// <summary>
    /// JSON path for this constraint. e.g. "d.pr"
    /// </summary>
    private readonly string _path;
    /// <summary>
    /// This field is required to be present in the JSON text.
    /// </summary>
    private readonly bool _isRequired;
    /// <summary>
    /// JSON type of the field. e.g. string or boolean
    /// </summary>
    private readonly JTokenType _type;
    /// <summary>
    /// required minimum number of children of this object (array or object).  If zero, number of children is not a constraint.
    /// </summary>
    private readonly int _requiredChildrenMinimum;
    /// <summary>
    /// required maximum number of children of this object (array or object).  If zero, number of children is not a constraint.
    /// </summary>
    private readonly int _requiredChildrenMaximum;
    /// <summary>
    /// Action to parse the JSON and update the DistributedTracePayload field with the parsed value.
    /// </summary>
    private readonly Action<JToken, T> _parse;

    /// <summary>
    /// Apply the constraints of this ValidationConstraint instance to the provided object and, after passing validation, parse the JSON and update the DistributedTracePayload instance.
    /// </summary>
    /// <param name="jObject">JSON object to parse</param>
    /// <param name="parsedPayload">DistributedTracePayload object to update</param>
    public void ParseAndThrowOnFailure(JToken jObject, T parsedPayload)
    {
        var selection = jObject.SelectToken(_path);

        if (null == selection)
        {
            if (_isRequired)
            {
                throw new DistributedTraceAcceptPayloadParseException($"expected to find {_path} {Enum.GetName(typeof(JTokenType), _type)}.");
            }
            //field is null and not required, just return.
            return;
        }

        if (selection.Type != _type)
        {
            throw new DistributedTraceAcceptPayloadParseException($"expected to find {_path} of type {Enum.GetName(typeof(JTokenType), _type)}. Actual type: {Enum.GetName(typeof(JTokenType), selection.Type)}");
        }

        if (0 < _requiredChildrenMinimum)
        {
            var countOfChildren = selection.Count();
            //test if both min and max child count are the same and produce an exception method without a range
            if (_requiredChildrenMinimum == _requiredChildrenMaximum && countOfChildren != _requiredChildrenMinimum)
            {
                throw new DistributedTraceAcceptPayloadParseException(
                    $"expected {_path} {Enum.GetName(typeof(JTokenType), _type)} to contain {_requiredChildrenMinimum} children. Found: {countOfChildren}");
            }
            if (countOfChildren < _requiredChildrenMinimum || countOfChildren > _requiredChildrenMaximum)
            {
                throw new DistributedTraceAcceptPayloadParseException(
                    $"expected {_path} {Enum.GetName(typeof(JTokenType), _type)} to contain {_requiredChildrenMinimum}-{_requiredChildrenMaximum} children. Found: {countOfChildren}");
            }
        }
        //if a parse Action was provided, call it to update the parsedPayload
        _parse?.Invoke(selection, parsedPayload);
    }

    public ValidationConstraint(string path, JTokenType type, bool isRequired, int miniumChildren, int maximumChildren, Action<JToken, T> parse)
    {
        _path = path;
        _isRequired = isRequired;
        _type = type;
        _requiredChildrenMaximum = maximumChildren;
        _requiredChildrenMinimum = miniumChildren;
        _parse = parse;
    }
}
