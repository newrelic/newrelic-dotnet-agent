// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Diagnostics;
using NewRelic.Agent.Core.Segments;

namespace NewRelic.Agent.Core.OpenTelemetryBridge
{
    /// <summary>
    /// A mock implementation of ActivitySource for testing RuntimeActivitySource
    /// </summary>
    public class MockActivitySource : IDisposable
    {
        public string Name { get; }
        public string Version { get; }
        public bool IsDisposed { get; private set; }
        
        public MockActivitySource(string name, string version)
        {
            Name = name;
            Version = version;
        }

        public void Dispose()
        {
            IsDisposed = true;
        }

        public virtual MockActivity CreateActivity(string name, MockActivityKind kind)
        {
            return new MockActivity
            {
                DisplayName = name,
                Kind = kind
            };
        }
    }

    /// <summary>
    /// Mock ActivityKind enum for testing
    /// </summary>
    public enum MockActivityKind
    {
        Internal = 0,
        Server = 1,
        Client = 2,
        Producer = 3,
        Consumer = 4
    }
    
    /// <summary>
    /// A mock implementation of Activity for testing RuntimeNewRelicActivity
    /// </summary>
    public class MockActivity
    {
        public virtual string DisplayName { get; set; }
        public virtual MockActivityKind Kind { get; set; }
        public virtual bool IsStopped { get; set; }
        public virtual MockSpanId SpanId { get; set; } = new MockSpanId();
        public virtual MockTraceId TraceId { get; set; } = new MockTraceId();
        private object _segment;
        
        public MockActivity()
        {
        }

        public virtual void Start()
        {
            IsStopped = false;
        }

        public virtual void Stop()
        {
            IsStopped = true;
        }

        public virtual void Dispose()
        {
            IsStopped = true;
        }

        public virtual object GetCustomProperty(string propertyName)
        {
            if (propertyName == NewRelicActivitySourceProxy.SegmentCustomPropertyName)
            {
                return _segment;
            }
            return null;
        }

        public virtual void SetCustomProperty(string propertyName, object value)
        {
            if (propertyName == NewRelicActivitySourceProxy.SegmentCustomPropertyName)
            {
                _segment = value;
            }
        }
    }

    /// <summary>
    /// Mock implementation of SpanId for testing
    /// </summary>
    public class MockSpanId
    {
        public override string ToString()
        {
            return "mock-span-id";
        }
    }

    /// <summary>
    /// Mock implementation of TraceId for testing
    /// </summary>
    public class MockTraceId
    {
        public override string ToString()
        {
            return "mock-trace-id";
        }
    }
}
