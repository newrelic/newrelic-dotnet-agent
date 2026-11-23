// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using System.Threading;
using NewRelic.Agent.Extensions.Logging;

namespace NewRelic.Agent.Core.Attributes;

/// <summary>
/// The full name and the allowed destinations of one dynamic attribute, resolved against the attribute filter.
/// </summary>
public sealed class ResolvedAttributeName
{
    private const int NotChecked = 0;
    private const int Valid = 1;
    private const int NotValid = 2;

    private readonly AttributeClassification _classification;
    private int _nameValidity;

    public string Name { get; }
    public AttributeDestinations Destinations { get; }

    public bool IsNameValid
    {
        get
        {
            var validity = Volatile.Read(ref _nameValidity);
            if (validity == NotChecked)
            {
                // Two threads can both validate; both write the same value.
                validity = Validate() ? Valid : NotValid;
                Volatile.Write(ref _nameValidity, validity);
            }

            return validity == Valid;
        }
    }

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
