// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Api.Experimental;

namespace NewRelic.Agent.Core.OpenTelemetryBridge
{
    // TODO: Not all of these properties on activities are available in all versions of the DiagnosticSource assembly.
    // We should either have code that gracefully handles the property or method not being available, or we need to
    // ensure that we only enable the bridging code when an appropriate minimum version of the DiagnosticSource
    // assembly is loaded.
    public class RuntimeNewRelicActivity : INewRelicActivity
    {
        private readonly object _activity;
        private readonly dynamic _dynamicActivity;

        public RuntimeNewRelicActivity(object activity)
        {
            _activity = activity;
            _dynamicActivity = (dynamic)_activity;
        }

        public bool IsStopped => (bool?)(_dynamicActivity)?.IsStopped ?? true;

        public string SpanId => (string)(_dynamicActivity)?.SpanId.ToString();

        public string TraceId => (string)(_dynamicActivity)?.TraceId.ToString();

        public string DisplayName => (string)(_dynamicActivity)?.DisplayName;

        public string Id => (string)(_dynamicActivity)?.Id;

        public void Dispose()
        {
            _dynamicActivity?.Dispose();
        }

        public void Start()
        {
            _dynamicActivity?.Start();
        }

        public void Stop()
        {
            _dynamicActivity?.Stop();
        }

        public void MakeCurrent()
        {
            ActivityBridgeHelpers.SetCurrentActivity(_activity);
        }

        public ISegment GetSegment()
        {
            return GetSegmentFromActivity(_activity);
        }

        public void SetSegment(ISegment segment)
        {
            ((dynamic)_activity)?.SetCustomProperty(NewRelicActivitySourceProxy.SegmentCustomPropertyName, segment);
        }

        public static ISegment GetSegmentFromActivity(object activity)
        {
            return ((dynamic)activity)?.GetCustomProperty(NewRelicActivitySourceProxy.SegmentCustomPropertyName) as ISegment;
        }
    }
}
