// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Core.Logging;
using System.Collections.Generic;
using System.Linq;

namespace NewRelic.Agent.Core.Wrapper
{
    /// <summary>
    /// A factory that returns wrappers for instrumented methods
    /// </summary>
    public interface IWrapperMap
    {
        /// <summary>
        /// Return a tracked wrapper that CanWrap the given method.
        /// </summary>
        TrackedWrapper Get(InstrumentedMethodInfo instrumentedMethodInfo);

        /// <summary>
        /// Returns the NoOp wrapper.
        /// </summary>
        /// <param name="instrumentedMethodInfo"></param>
        TrackedWrapper GetNoOpWrapper();
    }

    public class WrapperMap : IWrapperMap
    {
        private readonly List<IDefaultWrapper> _defaultWrappers;
        private readonly List<IWrapper> _nonDefaultWrappers;
        private readonly TrackedWrapper _noOpTrackedWrapper;

        public WrapperMap(IEnumerable<IWrapper> wrappers, IDefaultWrapper defaultWrapper, INoOpWrapper noOpWrapper)
        {
            _nonDefaultWrappers = wrappers
                .Where(wrapper => wrapper != null)
                .Where(wrapper => !(wrapper is IDefaultWrapper) && !(wrapper is INoOpWrapper))
                .ToList();

            _nonDefaultWrappers.Add(new AttachToAsyncWrapper());
            _nonDefaultWrappers.Add(new DetachWrapper());
            _nonDefaultWrappers.Add(new CustomSegmentWrapper());
            _nonDefaultWrappers.Add(new IgnoreTransactionWrapper());
            _nonDefaultWrappers.Add(new MultithreadedTrackingWrapper());
            _nonDefaultWrappers.Add(new OtherTransactionWrapper());

            // This allows instrumentation that does nothing other than to track the library version.
            _nonDefaultWrappers.Add(noOpWrapper);

            var defaultWrappers = new List<IDefaultWrapper> { defaultWrapper, new DefaultWrapperAsync() };

            _defaultWrappers = defaultWrappers;

            _noOpTrackedWrapper = new TrackedWrapper(noOpWrapper);

            if (wrappers.Count() == 0)
            {
                Log.Error("No wrappers were loaded.  The agent will not behave as expected.");
            }

            if (Log.IsFinestEnabled)
                Log.Finest("WrapperMap has NonDefaultWrappers: {0}", string.Join(", ", _nonDefaultWrappers));
        }

        public TrackedWrapper Get(InstrumentedMethodInfo instrumentedMethodInfo)
        {
            //Then, see if there's a standard wrapper supporting this method
            foreach (var wrapper in _nonDefaultWrappers)
            {
                if (CanWrap(instrumentedMethodInfo, wrapper))
                {
                    return new TrackedWrapper(wrapper);
                }
            }

            //Next, check to see if one of the dynamic wrappers can be used
            foreach (var wrapper in ExtensionsLoader.TryGetDynamicWrapperInstance(instrumentedMethodInfo.RequestedWrapperName))
            {
                if (CanWrap(instrumentedMethodInfo, wrapper))
                {
                    return new TrackedWrapper(wrapper);
                }
            }

            //Otherwise, return one of our defaults or a NoOp
            return GetDefaultWrapperOrSetNoOp(instrumentedMethodInfo);
        }

        public TrackedWrapper GetNoOpWrapper()
        {
            return _noOpTrackedWrapper;
        }

        private TrackedWrapper GetDefaultWrapperOrSetNoOp(InstrumentedMethodInfo instrumentedMethodInfo)
        {
            foreach (var wrapper in _defaultWrappers)
            {
                if (CanWrap(instrumentedMethodInfo, wrapper))
                {
                    return new TrackedWrapper(wrapper);
                }
            }

            Log.Debug(
                "No matching wrapper found for {0}.{1}({2}) in assembly [{3}] (requested wrapper name was {4}). This usually indicates misconfigured instrumentation. This method will be ignored.",
                instrumentedMethodInfo.Method.Type.FullName,
                instrumentedMethodInfo.Method.MethodName,
                instrumentedMethodInfo.Method.ParameterTypeNames,
                instrumentedMethodInfo.Method.Type.Assembly.FullName,
                instrumentedMethodInfo.RequestedWrapperName);

            return GetNoOpWrapper();
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
