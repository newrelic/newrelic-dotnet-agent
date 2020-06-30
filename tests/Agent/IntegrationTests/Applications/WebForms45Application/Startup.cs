/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/

using Microsoft.Owin;
using Owin;

[assembly: OwinStartupAttribute(typeof(WebForms45Application.Startup))]
namespace WebForms45Application
{
    public partial class Startup
    {
        public void Configuration(IAppBuilder app)
        {

        }
    }
}
