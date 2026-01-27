// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NewRelic.Agent.Core.Tracer;
using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Agent.Core.Wrapper;

/// <summary>
/// A simple class which wraps an AfterWrappedMethodDelegate in an ITracer so that it can be easily passed around in a system already designed to handle ITracers
/// </summary>
public class WrapperTracer : ITracer
{
    private readonly AfterWrappedMethodDelegate _afterWrappedMethodDelegate;

    public WrapperTracer(AfterWrappedMethodDelegate afterWrappedMethodDelegate)
    {
        _afterWrappedMethodDelegate = afterWrappedMethodDelegate;
    }

    public void Finish(object returnValue, Exception exception)
    {
        _afterWrappedMethodDelegate(returnValue, exception);
    }
}