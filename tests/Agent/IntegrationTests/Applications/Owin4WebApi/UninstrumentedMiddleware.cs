// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.Threading.Tasks;
using Microsoft.Owin;

namespace Owin4WebApi;

public class UninstrumentedMiddleware : OwinMiddleware
{
    public UninstrumentedMiddleware(OwinMiddleware next) : base(next)
    {
    }

    public override async Task Invoke(IOwinContext context)
    {
        await Task.Delay(1);
        await Next.Invoke(context);
    }
}