// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;

namespace NewRelic.Agent.Core.Attributes;

/// <summary>
/// The one shared definition of a dynamic attribute family. The attribute name comes from the caller on each set.
/// </summary>
public class DynamicAttributeDefinition<TInput, TOutput> : AttributeDefinition
{
    private readonly Func<TInput, TOutput> _conversionImpl;
    private readonly Func<TOutput, TOutput> _postProcessingImpl;
    private readonly Func<string, ResolvedAttributeName> _resolver;

    public DynamicAttributeDefinition(string familyLabel, AttributeClassification classification, Func<TInput, TOutput> convert, Func<TOutput, TOutput> postProcess, Func<string, ResolvedAttributeName> resolver)
        : base(familyLabel, classification, new Dictionary<AttributeDestinations, bool>())
    {
        _conversionImpl = convert;
        _postProcessingImpl = postProcess;
        _resolver = resolver;
    }

    public ResolvedAttributeName Resolve(string rawName)
    {
        if (rawName == null)
        {
            throw new ArgumentNullException(nameof(rawName));
        }

        return _resolver(rawName);
    }

    /// <returns>true if the value was set</returns>
    public bool TrySetValue(IAttributeValueCollection collection, string rawName, TInput value)
    {
        var resolved = Resolve(rawName);

        if (!resolved.IsNameValid || collection.IsImmutable || !resolved.IsAvailableForAny(collection.TargetModelTypes))
        {
            return false;
        }

        if (!AttributeValueConverter.TryConvert(value, _conversionImpl, _postProcessingImpl, out var converted))
        {
            LogNullValue(resolved.Destinations, Classification, resolved.Name);
            return false;
        }

        return collection.TrySetValue(this, resolved, converted);
    }

    /// <summary>
    /// Sets the value as it is: no name check, no availability check, and no conversion.
    /// </summary>
    /// <returns>true if the value was set</returns>
    public bool TrySetValueUnchecked(IAttributeValueCollection collection, string rawName, TOutput value)
    {
        var resolved = Resolve(rawName);

        return collection.TrySetValue(this, resolved, value);
    }
}
