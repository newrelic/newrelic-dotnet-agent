using System;
using JetBrains.Annotations;

namespace NewRelic.Agent.Extensions.Providers.Wrapper
{
	public class MethodSignature
	{
		public String MethodName;
		public String ParameterSignature;
		public MethodSignature([NotNull] String methodName, [CanBeNull] String parameterSignature = null)
		{
			MethodName = methodName;
			ParameterSignature = parameterSignature;
		}
	}
}
