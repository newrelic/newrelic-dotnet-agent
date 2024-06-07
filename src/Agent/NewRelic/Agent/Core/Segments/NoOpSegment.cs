// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NewRelic.Agent.Api;
using NewRelic.Agent.Api.Experimental;
using NewRelic.Agent.Core.Attributes;

namespace NewRelic.Agent.Core.Segments
{
    public class NoOpSegment : ISegment, ISegmentExperimental, ISegmentDataState
    {
        private static ISegmentData _noOpSegmentDataImpl;
        private ISegmentData _noOpSegmentData => _noOpSegmentDataImpl ?? (_noOpSegmentDataImpl = new SimpleSegmentData("NoOpSegment"));

        private static SpanAttributeValueCollection _attribValCollectionImpl;
        private SpanAttributeValueCollection _attribValCollection => _attribValCollectionImpl ?? (_attribValCollectionImpl = new SpanAttributeValueCollection());

        private readonly IAttributeDefinitions _attribDefs = new AttributeDefinitions(new AttributeFilter(new AttributeFilter.Settings()));

        public bool IsDone => true; // the segment is technically done since it is does nothing.
        public bool IsValid => false;
        public bool DurationShouldBeDeductedFromParent { get; set; } = false;
        public bool AlwaysDeductChildDuration { private get; set; } = false;
        public bool IsLeaf => false;
        public bool IsExternal => false;
        public string SpanId => null;

        public ISegmentData SegmentData => _noOpSegmentData;

        public IAttributeDefinitions AttribDefs => _attribDefs;

        public SpanAttributeValueCollection AttribValues => _attribValCollection;

        public string TypeName => string.Empty;

        public string UserCodeFunction { get => string.Empty; set { } }
        public string UserCodeNamespace { get => string.Empty; set { } }

		public string SegmentNameOverride { get; set; } = null;
		
        public void End() { }
        public void End(Exception ex) { }
        public void EndStackExchangeRedis() { }
        public void MakeCombinable() { }

        public ISegmentExperimental MakeLeaf()
        {
            return this;
        }

        public void RemoveSegmentFromCallStack() { }
        public void SetMessageBrokerDestination(string destination) { }

        public ISegmentExperimental SetSegmentData(ISegmentData segmentData)
        {
            return this;
        }

        public ISpan AddCustomAttribute(string key, object value)
        {
            return this;
        }

        public ISpan SetName(string name)
        {
            return this;
        }

        public string GetCategory()
        {
            return string.Empty;
        }

        public TimeSpan DurationOrZero => TimeSpan.Zero;
    }
}
