/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
namespace NewRelic.Agent.Extensions.Providers.Wrapper
{
    public class MethodSignature
    {
        public string MethodName;
        public string ParameterSignature;
        public MethodSignature(string methodName, string parameterSignature = null)
        {
            MethodName = methodName;
            ParameterSignature = parameterSignature;
        }
    }
}
