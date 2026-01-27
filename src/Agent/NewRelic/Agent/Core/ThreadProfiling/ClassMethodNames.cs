// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.Agent.Core.ThreadProfiling;

public class ClassMethodNames
{
    public readonly string Class;
    public readonly string Method;

    public ClassMethodNames(string @class, string method)
    {
        Class = @class;
        Method = method;
    }
}
