// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace AspNetCoreWebApiLambdaApplication
{
    public class APIGatewayProxyFunctionEntryPoint : Amazon.Lambda.AspNetCoreServer.APIGatewayProxyFunction
    {
        protected override void Init(IWebHostBuilder builder)
        {
            builder
                .UseStartup<Startup>();
        }

        protected override void Init(IHostBuilder builder)
        {
        }
    }

    public class ApplicationLoadBalancerFunctionEntryPoint :Amazon.Lambda.AspNetCoreServer.ApplicationLoadBalancerFunction
    {
        protected override void Init(IWebHostBuilder builder)
        {
            builder.UseStartup<Startup>();
        }

        protected override void Init(IHostBuilder builder)
        {
        }
    }

    public class APIGatewayHttpApiV2ProxyFunctionEntryPoint : Amazon.Lambda.AspNetCoreServer.APIGatewayHttpApiV2ProxyFunction
    {
        protected override void Init(IWebHostBuilder builder)
        {
            builder.UseStartup<Startup>();
        }

        protected override void Init(IHostBuilder builder)
        {
        }
    }
}
