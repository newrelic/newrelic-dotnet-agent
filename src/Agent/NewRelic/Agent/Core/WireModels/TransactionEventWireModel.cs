// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Core.Attributes;

namespace NewRelic.Agent.Core.WireModels;

public class TransactionEventWireModel : EventWireModel
{
    public TransactionEventWireModel(IAttributeValueCollection attribValues, bool isSynthetics, float priority)
        : base(AttributeDestinations.TransactionEvent, attribValues, isSynthetics, priority)
    {
    }
}