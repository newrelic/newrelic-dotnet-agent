namespace NewRelic.Agent.Extensions.Providers.Wrapper
{
	/// <summary>
	/// Holds method invocation arguments.
	/// </summary>
	public class MethodCall
	{
		public readonly Method Method;
		public readonly object InvocationTarget;
		public readonly object[] MethodArguments;

		public MethodCall(Method method, object invocationTarget, object[] methodArguments)
		{
			Method = method;
			InvocationTarget = invocationTarget;
			MethodArguments = methodArguments ?? new object[0];
		}
	}
}
