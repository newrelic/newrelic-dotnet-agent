﻿using System;
using JetBrains.Annotations;

namespace NewRelic.Agent.Extensions.Providers.Wrapper
{
	// ReSharper disable once InconsistentNaming
	public static class IAgentWrapperExtensions
	{
		public static void HandleExceptions([NotNull] this IAgentWrapperApi agentWrapperApi, [NotNull] Action action)
		{
			try
			{
				action();
			}
			catch (Exception ex)
			{
				agentWrapperApi.SafeHandleException(ex);
			}
		}

		/// <summary>
		/// Attempts to call agentWrapperApi.HandleWrapperException(ex). Catches and swallows any exceptions
		/// to prevent them from harming instrumented application.
		/// </summary>
		/// <param name="agentWrapperApi"></param>
		/// <param name="ex"></param>
		public static void SafeHandleException([NotNull] this IAgentWrapperApi agentWrapperApi, Exception ex)
		{
			try
			{
				agentWrapperApi.HandleWrapperException(ex);
			}
			catch
			{
				// Not much more we can do here. Prevent exception from harming instrumented application.
			}
		}
	}
}
