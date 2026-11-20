// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using NewRelic.Agent.Extensions.Logging;

namespace NewRelic.Agent.Core.Attributes;

/// <summary>
/// The full name and the allowed destinations of one dynamic attribute, resolved against the attribute filter.
/// </summary>
public sealed class ResolvedAttributeName
{
    private readonly AttributeClassification _classification;
    private bool? _isNameValid;

    public string Name { get; }
    public AttributeDestinations Destinations { get; }
    public bool IsNameValid => _isNameValid ?? (_isNameValid = Validate()).Value;

    public ResolvedAttributeName(string name, AttributeClassification classification, AttributeDestinations destinations)
    {
        Name = name;
        _classification = classification;
        Destinations = destinations;
    }

    public bool IsAvailableForAny(AttributeDestinations[] targetModels)
    {
        if (targetModels == null)
        {
            return false;
        }

        foreach (var targetModel in targetModels)
        {
            if ((Destinations & targetModel) != AttributeDestinations.None)
            {
                return true;
            }
        }

        return false;
    }

    private bool Validate()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            Log.Debug($"{Destinations} {_classification} Attribute definition is not valid - Name is null/empty");
            return false;
        }

        if (Encoding.UTF8.GetByteCount(Name) > AttributeDefinition.AttribNameMaxLengthBytes)
        {
            Log.Debug($"{Destinations} {_classification} Attribute definition is not valid - Name is is too large ({Name})");
            return false;
        }

        return true;
    }
}
