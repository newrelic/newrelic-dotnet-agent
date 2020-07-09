﻿using System;
using System.Linq;
using JetBrains.Annotations;
using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Agent.Core.Wrapper
{
	// This interface needed for DI/mocking purposes.
	public interface IDefaultWrapper : IWrapper
	{
		
	}

	public class DefaultWrapper : IDefaultWrapper
	{
		[NotNull]
		private static readonly string[] PossibleWrapperNames = {
			"NewRelic.Agent.Core.Wrapper.DefaultWrapper",
			"NewRelic.Providers.Wrapper.CustomInstrumentationAsync.DefaultWrapperAsync",

			// To support older custom instrumentation we need to also accept the old tracer factory name
			"NewRelic.Agent.Core.Tracer.Factories.DefaultTracerFactory"
		};

		public bool IsTransactionRequired => true;

		public CanWrapResponse CanWrap(InstrumentedMethodInfo instrumentedMethodInfo)
		{
			var canWrap = !instrumentedMethodInfo.IsAsync
				&& PossibleWrapperNames.Contains(instrumentedMethodInfo.RequestedWrapperName);

			return new CanWrapResponse(canWrap);
		}

		public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgentWrapperApi agentWrapperApi, ITransaction transaction)
		{
			var typeName = instrumentedMethodCall.MethodCall.Method.Type.FullName ?? "<unknown>";
			var methodName = instrumentedMethodCall.MethodCall.Method.MethodName;
			var segment = !String.IsNullOrEmpty(instrumentedMethodCall.RequestedMetricName)
				? transaction.StartCustomSegment(instrumentedMethodCall.MethodCall, instrumentedMethodCall.RequestedMetricName)
				: transaction.StartMethodSegment(instrumentedMethodCall.MethodCall, typeName, methodName);

			if (!String.IsNullOrEmpty(instrumentedMethodCall.RequestedMetricName) && instrumentedMethodCall.RequestedTransactionNamePriority.HasValue)
			{
				transaction.SetCustomTransactionName(instrumentedMethodCall.RequestedMetricName, instrumentedMethodCall.RequestedTransactionNamePriority.Value);
			}

			return Delegates.GetDelegateFor(segment);
		}
	}
}
