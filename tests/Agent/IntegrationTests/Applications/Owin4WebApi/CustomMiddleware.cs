// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.Owin;
using NewRelic.Api.Agent;

namespace Owin4WebApi;

public class CustomMiddleware : OwinMiddleware
{
    public CustomMiddleware(OwinMiddleware next) : base(next)
    {
    }

    [Trace]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public override async Task Invoke(IOwinContext context)
    {
        await MiddlewareMethodAsync();
        await Next.Invoke(context);
    }

    [Trace]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private async Task MiddlewareMethodAsync()
    {
        await Task.Delay(1);
    }

}