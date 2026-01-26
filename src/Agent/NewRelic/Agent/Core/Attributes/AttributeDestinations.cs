// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;

namespace NewRelic.Agent.Core.Attributes;

[Flags]
public enum AttributeDestinations : byte
{
    None = 0,
    TransactionTrace = 1 << 0,
    TransactionEvent = 1 << 1,
    ErrorTrace = 1 << 2,
    JavaScriptAgent = 1 << 3,
    ErrorEvent = 1 << 4,
    SqlTrace = 1 << 5,
    SpanEvent = 1 << 6,
    CustomEvent = 1 << 7,
    All = 0xFF,
}