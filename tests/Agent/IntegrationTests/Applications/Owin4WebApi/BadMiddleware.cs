// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using Microsoft.Owin;
using System.Threading.Tasks;

namespace Owin4WebApi
{
    public class BadMiddleware : OwinMiddleware
    {
        public BadMiddleware(OwinMiddleware next) : base(next)
        {
        }

#pragma warning disable 1998
        public override async Task Invoke(IOwinContext context)
        {
            throw new ArgumentException("This exception is from the BadMiddleware");
        }
#pragma warning restore 1998

    }
}
