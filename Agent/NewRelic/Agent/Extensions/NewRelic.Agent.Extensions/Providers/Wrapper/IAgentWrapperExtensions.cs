using System;

namespace NewRelic.Agent.Extensions.Providers.Wrapper
{
	// ReSharper disable once InconsistentNaming
	public static class IAgentWrapperExtensions
	{
		public static void HandleExceptions(this IAgent agent, Action action)
		{
			try
			{
				action();
			}
			catch (Exception ex)
			{
				agent.SafeHandleException(ex);
			}
		}

		/// <summary>
		/// Attempts to call agent.HandleWrapperException(ex). Catches and swallows any exceptions
		/// to prevent them from harming instrumented application.
		/// </summary>
		/// <param name="agent"></param>
		/// <param name="ex"></param>
		public static void SafeHandleException(this IAgent agent, Exception ex)
		{
			try
			{
				agent.HandleWrapperException(ex);
			}
			catch
			{
				// Not much more we can do here. Prevent exception from harming instrumented application.
			}
		}
	}
}
