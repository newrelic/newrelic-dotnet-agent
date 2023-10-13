// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Linq;
using System.Threading;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Providers.Wrapper.AspNetCore6Plus
{
    public class GenericHostWebHostBuilderExtensionsWrapper6Plus : IWrapper
    {
        public bool IsTransactionRequired => false;
        private static int _isNewRelicStartupFilterAdded;

        public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
        {
            return new CanWrapResponse(nameof(GenericHostWebHostBuilderExtensionsWrapper6Plus).Equals(methodInfo.RequestedWrapperName));
        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
        {
            AddStartupFilterToHostBuilder(instrumentedMethodCall.MethodCall.MethodArguments[0], agent);

            return Delegates.NoOp;
        }

        public void AddStartupFilterToHostBuilder(object hostBuilder, IAgent agent)
        {
            if (0 == Interlocked.CompareExchange(ref _isNewRelicStartupFilterAdded, 1, 0))
            {
                var typedHostBuilder = (Microsoft.Extensions.Hosting.IHostBuilder)hostBuilder;
                typedHostBuilder.ConfigureServices(AddStartupFilter);

                void AddStartupFilter(Microsoft.Extensions.Hosting.HostBuilderContext hostBuilderContext, IServiceCollection services)
                {
                    //Forced evaluation is important. Do not remove ToList()
                    var startupFilters = services.Where(serviceDescriptor => serviceDescriptor.ServiceType == typeof(IStartupFilter)).ToList();

                    services.AddTransient<IStartupFilter>(provider => new AddNewRelicStartupFilter(agent));

                    foreach (var filter in startupFilters)
                    {
                        services.Remove(filter); // Remove from early in pipeline
                        services.Add(filter); // Add to end after our AddNewRelicStartupFilter
                    }
                }
            }
        }
    }
}
