using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using NewRelic.Agent.Core.Logging;
using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Agent.Core.Wrapper
{
	/// <summary>
	/// A map that links instrumented methods to wrappers that can wrap those methods. Intentionally excludes DefaultWrapper.
	/// </summary>
	public interface IWrapperMap
	{
		/// <summary>
		/// Get the tracked wrapper that is mapped to the given method.
		/// </summary>
		[NotNull]
		TrackedWrapper Get(InstrumentedMethodInfo instrumentedMethodInfo);

		/// <summary>
		/// Overrides whatever value is currently cached for the given key.
		/// </summary>
		void Override(InstrumentedMethodInfo instrumentedMethodInfo, [CanBeNull] TrackedWrapper trackedWrapper);

		/// <summary>
		/// Overrides the instrumented method to use the NoOp wrapper.
		/// </summary>
		/// <param name="instrumentedMethodInfo"></param>
		TrackedWrapper SetNoOpWrapper(InstrumentedMethodInfo instrumentedMethodInfo);
	}

	public class WrapperMap : IWrapperMap
	{
		[NotNull]
		private readonly LazyMap<InstrumentedMethodInfo, TrackedWrapper> _lazyMap;

		private readonly IList<IDefaultWrapper> _defaultWrappers;

		private readonly TrackedWrapper _noOpTrackedWrapper;

		public WrapperMap([NotNull] IEnumerable<IWrapper> wrappers, [NotNull] IDefaultWrapper defaultWrapper, [NotNull] INoOpWrapper noOpWrapper)
		{
			var trackedWrappers = wrappers
				.Where(wrapper => wrapper != null)
				// DefaultWrapper intentionally does not get included in the wrapper map because it is specially handled by WrapperService
				.Where(wrapper => !(wrapper is IDefaultWrapper) && !(wrapper is INoOpWrapper))
				.Select(wrapper => new TrackedWrapper(wrapper)).ToList();

			_defaultWrappers = new List<IDefaultWrapper> {defaultWrapper};

			var otherDefaultWrappers = wrappers
				.OfType<IDefaultWrapper>()
				.Where(wrapper => !(wrapper is DefaultWrapper));

			foreach (var wrapper in otherDefaultWrappers)
			{
				_defaultWrappers.Add(wrapper);
			}

			_noOpTrackedWrapper = new TrackedWrapper(noOpWrapper);

			_lazyMap = new LazyMap<InstrumentedMethodInfo, TrackedWrapper>(trackedWrappers, CanWrap);
			if (wrappers.Count() == 0)
			{
				Log.Error("No wrappers were loaded.  The agent will not behave as expected.");
			}
		}

		public TrackedWrapper Get(InstrumentedMethodInfo instrumentedMethodInfo)
		{
			return _lazyMap.Get(instrumentedMethodInfo) ?? GetDefaultWrapperOrSetNoOp(instrumentedMethodInfo);
		}

		public void Override(InstrumentedMethodInfo method, TrackedWrapper trackedWrapper)
		{
			_lazyMap.Override(method, trackedWrapper);
		}

		public TrackedWrapper SetNoOpWrapper(InstrumentedMethodInfo method)
		{
			Override(method, _noOpTrackedWrapper);
			return _noOpTrackedWrapper;
		}

		private TrackedWrapper GetDefaultWrapperOrSetNoOp(InstrumentedMethodInfo instrumentedMethodInfo)
		{
			TrackedWrapper trackedWrapper;

			var defaultWrapper = _defaultWrappers.FirstOrDefault(wrapper => wrapper.CanWrap(instrumentedMethodInfo).CanWrap);

			if (defaultWrapper != null)
			{
				trackedWrapper = new TrackedWrapper(defaultWrapper);
				Override(instrumentedMethodInfo, trackedWrapper);
			}
			else
			{
				Log.DebugFormat("No matching wrapper found for {0}.{1}({2}) in assembly [{3}] (requested wrapper name was {4}). This usually indicates misconfigured instrumentation. This method will be ignored.",
					instrumentedMethodInfo.Method.Type.FullName,
					instrumentedMethodInfo.Method.MethodName,
					instrumentedMethodInfo.Method.ParameterTypeNames,
					instrumentedMethodInfo.Method.Type.Assembly.FullName,
					instrumentedMethodInfo.RequestedWrapperName);

				return SetNoOpWrapper(instrumentedMethodInfo);
			}

			return trackedWrapper;
		}

		private static Boolean CanWrap(InstrumentedMethodInfo instrumentedMethodInfo, [CanBeNull] TrackedWrapper trackedWrapper)
		{
			if (trackedWrapper == null)
				return false;

			var method = instrumentedMethodInfo.Method;
			var canWrapResponse = trackedWrapper.Wrapper.CanWrap(instrumentedMethodInfo);

			if (canWrapResponse.AdditionalInformation != null && !canWrapResponse.CanWrap)
				Log.Warn(canWrapResponse.AdditionalInformation);
			if (canWrapResponse.AdditionalInformation != null && canWrapResponse.CanWrap)
				Log.Info(canWrapResponse.AdditionalInformation);

			if (canWrapResponse.CanWrap)
				Log.Debug($"Wrapper \"{trackedWrapper.Wrapper.GetType().FullName}\" will be used for instrumented method \"{method.Type}.{method.MethodName}\"");

			return canWrapResponse.CanWrap;
		}
	}
}
