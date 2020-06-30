/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/

using Microsoft.Owin;
using System.Threading.Tasks;

namespace Owin3WebApi
{
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
}
