// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;

namespace NewRelic.Agent.Core.ThreadProfiling;

public class InvalidProfileIdException : Exception
{
    public InvalidProfileIdException(string message)
        : base(message)
    { }
}