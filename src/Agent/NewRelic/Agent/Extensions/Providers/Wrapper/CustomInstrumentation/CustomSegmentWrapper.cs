// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Linq;
using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Providers.Wrapper.CustomInstrumentation
{
    public class CustomSegmentWrapper : IWrapper
    {
        private static readonly string[] PossibleWrapperNames = {
            "NewRelic.Providers.Wrapper.CustomInstrumentation.CustomSegmentWrapper",

            // To support older custom instrumentation we need to also accept the old tracer factory name
            "NewRelic.Agent.Core.Tracer.Factories.CustomSegmentTracerFactory"
        };

        public bool IsTransactionRequired => true;

        public CanWrapResponse CanWrap(InstrumentedMethodInfo instrumentedMethodInfo)
        {
            var canWrap = PossibleWrapperNames.Contains(instrumentedMethodInfo.RequestedWrapperName);
            return new CanWrapResponse(canWrap);
        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgentWrapperApi agentWrapperApi, ITransaction transaction)
        {
            // find the first string argument
            string segmentName = null;
            foreach (var argument in instrumentedMethodCall.MethodCall.MethodArguments)
            {
                segmentName = argument as string;
                if (segmentName != null)
                    break;
            }

            if (segmentName == null)
            {
                throw new ArgumentException("The CustomSegmentWrapper can only be applied to a method with a String parameter.");
            }

            var segment = transaction.StartCustomSegment(instrumentedMethodCall.MethodCall, segmentName);

            return Delegates.GetDelegateFor(segment);
        }
    }
}
