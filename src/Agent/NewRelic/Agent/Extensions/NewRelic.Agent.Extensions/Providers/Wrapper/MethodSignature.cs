using System;

namespace NewRelic.Agent.Extensions.Providers.Wrapper
{
    public class MethodSignature
    {
        public String MethodName;
        public String ParameterSignature;
        public MethodSignature(String methodName, String parameterSignature = null)
        {
            MethodName = methodName;
            ParameterSignature = parameterSignature;
        }
    }
}
