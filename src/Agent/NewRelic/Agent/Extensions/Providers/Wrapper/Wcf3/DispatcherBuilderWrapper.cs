/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using System;
using System.Collections.Generic;
using NewRelic.Agent.Api.Experimental;
using System.ServiceModel.Description;

namespace NewRelic.Providers.Wrapper.Wcf3
{
    public class DispatcherBuilderWrapper : IWrapper
    {
        private const string AssemblyName = "System.ServiceModel";
        private const string TypeName = "System.ServiceModel.Description.DispatcherBuilder";
        private const string MethodName = "InitializeServiceHost";

        public bool IsTransactionRequired => false;
        private static readonly object _bindingLock = new object();

        public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
        {
            var method = methodInfo.Method;
            var canWrap = method.MatchesAny
            (
                assemblyName: AssemblyName,
                typeName: TypeName,
                methodName: MethodName
            );
            return new CanWrapResponse(canWrap);
        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
        {
            return Delegates.GetDelegateFor(onComplete: () =>
            {
                var serviceDescription = instrumentedMethodCall.MethodCall.MethodArguments[0] as ServiceDescription;

                lock (_bindingLock)
                {
                    var bindingsSent = new List<Type>();
                    foreach (var endpoint in serviceDescription.Endpoints)
                    {
                        var bindingType = endpoint.Binding.GetType();
                        if (!bindingsSent.Contains(bindingType))
                        {
                            bindingsSent.Add(bindingType);
                            if (!SystemBindingTypes.Contains(bindingType))
                            {
                                agent.GetExperimentalApi().RecordSupportabilityMetric("WCFService/BindingType/CustomBinding");
                            }
                            else
                            {
                                agent.GetExperimentalApi().RecordSupportabilityMetric($"WCFService/BindingType/{bindingType.Name}");
                            }
                        }
                    }
                }
            });
        }
    }
}
