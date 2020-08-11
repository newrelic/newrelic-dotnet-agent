// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;

namespace NewRelic.Agent.Extensions.Providers
{
    /// <summary>
    /// This attribute indicates that a type is not serialiable on its own.
    /// If it is used in a context that can be serialized, such as CallContext logical data,
    /// it should be wrapped in a serializable container.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface)]
    public class NeedSerializableContainer : Attribute
    {

    }
}
