using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using NewRelic.Agent.Core.Logging;
using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Agent.Core.Wrapper
{
	/// <summary>
	/// A factory that returns wrappers for instrumented methods.
	/// </summary>
	public interface IWrapperMap
	{
		/// <summary>
		/// Return a tracked wrapper that CanWrap the given method.
		/// </summary>
		[NotNull]
		TrackedWrapper Get(InstrumentedMethodInfo instrumentedMethodInfo);

		/// <summary>
		/// Returns the NoOp wrapper.
		/// </summary>
		/// <param name="instrumentedMethodInfo"></param>
		TrackedWrapper GetNoOpWrapper();
	}

	public class WrapperMap : IWrapperMap
	{
		private readonly IEnumerable<IDefaultWrapper> _defaultWrappers;
		private readonly IEnumerable<IWrapper> _nonDefaultWrappers;

		private readonly TrackedWrapper _noOpTrackedWrapper;

		public WrapperMap([NotNull] IEnumerable<IWrapper> wrappers, [NotNull] IDefaultWrapper defaultWrapper, [NotNull] INoOpWrapper noOpWrapper)
		{
			_nonDefaultWrappers = wrappers
				.Where(wrapper => wrapper != null)
				.Where(wrapper => !(wrapper is IDefaultWrapper) && !(wrapper is INoOpWrapper));

			var defaultWrappers = new List<IDefaultWrapper> {defaultWrapper, new DefaultWrapperAsync()};

			_defaultWrappers = defaultWrappers;

			_noOpTrackedWrapper = new TrackedWrapper(noOpWrapper);

			if (wrappers.Count() == 0)
			{
				Log.Error("No wrappers were loaded.  The agent will not behave as expected.");
			}
		}

		public TrackedWrapper Get(InstrumentedMethodInfo instrumentedMethodInfo)
		{
			foreach (var wrapper in _nonDefaultWrappers)
			{
				if (CanWrap(instrumentedMethodInfo, wrapper))
				{
					return new TrackedWrapper(wrapper);
				}
			}
			return GetDefaultWrapperOrSetNoOp(instrumentedMethodInfo);
		}

		public TrackedWrapper GetNoOpWrapper()
		{
			return _noOpTrackedWrapper;
		}

		private TrackedWrapper GetDefaultWrapperOrSetNoOp(InstrumentedMethodInfo instrumentedMethodInfo)
		{
			var defaultWrapper = _defaultWrappers.FirstOrDefault(wrapper => wrapper.CanWrap(instrumentedMethodInfo).CanWrap);

			if (defaultWrapper != null)
			{
				return new TrackedWrapper(defaultWrapper);
			}
			else
			{
				Log.DebugFormat("No matching wrapper found for {0}.{1}({2}) in assembly [{3}] (requested wrapper name was {4}). This usually indicates misconfigured instrumentation. This method will be ignored.",
					instrumentedMethodInfo.Method.Type.FullName,
					instrumentedMethodInfo.Method.MethodName,
					instrumentedMethodInfo.Method.ParameterTypeNames,
					instrumentedMethodInfo.Method.Type.Assembly.FullName,
					instrumentedMethodInfo.RequestedWrapperName);

				return _noOpTrackedWrapper;
			}
		}

		private static bool CanWrap(InstrumentedMethodInfo instrumentedMethodInfo, IWrapper wrapper)
		{
			var method = instrumentedMethodInfo.Method;
			var canWrapResponse = wrapper.CanWrap(instrumentedMethodInfo);

			if (canWrapResponse.AdditionalInformation != null && !canWrapResponse.CanWrap)
				Log.Warn(canWrapResponse.AdditionalInformation);
			if (canWrapResponse.AdditionalInformation != null && canWrapResponse.CanWrap)
				Log.Info(canWrapResponse.AdditionalInformation);

			if (canWrapResponse.CanWrap)
				Log.Debug($"Wrapper \"{wrapper.GetType().FullName}\" will be used for instrumented method \"{method.Type}.{method.MethodName}\"");

			return canWrapResponse.CanWrap;
		}
	}
}
