/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
using System.Collections.Generic;
using System.Linq;
using System;
using NewRelic.Agent.Core.Attributes;
using Google.Protobuf.Collections;
using Newtonsoft.Json;
using NewRelic.Agent.Core.JsonConverters;
using NewRelic.Collections;
using NewRelic.Agent.Core.DataTransport;
using NewRelic.Agent.Core.WireModels;

namespace NewRelic.Agent.Core.Segments
{
    public partial class Span : IStreamingModel
    {
        public string SpanId { get; set; }

        public string DisplayName => $"{TraceId}.{SpanId}";
    }

    public partial class SpanBatch : IStreamingBatchModel<Span>
    {
        public int Count => (Spans?.Count).GetValueOrDefault(0);
    }

    [JsonConverter(typeof(SpanEventWireModelSerializer))]
    public interface ISpanEventWireModel : IAttributeValueCollection, IHasPriority
    {
        Span Span { get; }
    }

    public class SpanAttributeValueCollection : AttributeValueCollectionBase<AttributeValue>, ISpanEventWireModel
    {
        public float Priority { get; set; }

        //Since the Map Field may not be concurrrent, we need to lock the objects when performing operations around them
        private readonly Dictionary<AttributeClassification, object> _lockObjects = new Dictionary<AttributeClassification, object>
        {
            { AttributeClassification.AgentAttributes, new object() },
            { AttributeClassification.Intrinsics, new object() },
            { AttributeClassification.UserAttributes, new object() }
        };

        private readonly Span _span;
        public Span Span => _span;

        public SpanAttributeValueCollection() : base(AttributeDestinations.SpanEvent)
        {
            _span = new Span();
        }

        protected override IEnumerable<AttributeValue> GetAttribValuesImpl(AttributeClassification classification)
        {
            return GetAttribValuesInternal(classification).Values;
        }

        protected override bool SetValueImpl(IAttributeValue value)
        {
            var attribVal = value is AttributeValue
                        ? (AttributeValue)value
                        : new AttributeValue(value);

            return SetValueInternal(attribVal);
        }

        protected override bool SetValueImpl(AttributeDefinition attribDef, object value)
        {
            var attribVal = new AttributeValue(attribDef);
            attribVal.Value = value;

            return SetValueInternal(attribVal);
        }

        protected override bool SetValueImpl(AttributeDefinition attribDef, Lazy<object> lazyValue)
        {
            var attribVal = new AttributeValue(attribDef);
            attribVal.LazyValue = lazyValue;

            return SetValueInternal(attribVal);
        }

        protected override void RemoveItemsImpl(IEnumerable<AttributeValue> itemsToRemove)
        {
            foreach (var lockObjKVP in _lockObjects)
            {
                var keysToRemoveForClassification = itemsToRemove
                    .Where(x => x.AttributeDefinition.Classification == lockObjKVP.Key)
                    .Select(x => x.AttributeDefinition.Name)
                    .ToArray();

                if (keysToRemoveForClassification.Length == 0)
                {
                    continue;
                }

                var dicForClassification = GetAttribValuesInternal(lockObjKVP.Key);

                lock (lockObjKVP.Value)
                {
                    foreach (var keyToRemove in keysToRemoveForClassification)
                    {
                        dicForClassification.Remove(keyToRemove);
                    }
                }
            }

        }

        private bool SetValueInternal(AttributeValue attribVal)
        {
            //These values are used to create a DisplayName on the Streamable Object
            switch (attribVal.AttributeDefinition.Name)
            {
                case AttributeDefinition.KeyName_TraceId:
                    Span.TraceId = attribVal.StringValue;
                    break;

                case AttributeDefinition.KeyName_Guid:
                    Span.SpanId = attribVal.StringValue;
                    break;
            }

            var dic = GetAttribValuesInternal(attribVal.AttributeDefinition.Classification);

            if (dic == null)
            {
                return false;
            }

            var lockObj = _lockObjects[attribVal.AttributeDefinition.Classification];

            lock (lockObj)
            {
                var hasItem = dic.ContainsKey(attribVal.AttributeDefinition.Name);

                dic[attribVal.AttributeDefinition.Name] = attribVal;

                return !hasItem;
            }
        }

        private MapField<string, AttributeValue> GetAttribValuesInternal(AttributeClassification classification)
        {
            switch (classification)
            {
                case AttributeClassification.AgentAttributes:
                    return _span.AgentAttributes;
                case AttributeClassification.UserAttributes:
                    return _span.UserAttributes;
                case AttributeClassification.Intrinsics:
                    return _span.Intrinsics;
                default:
                    return null;
            }
        }
    }
}
