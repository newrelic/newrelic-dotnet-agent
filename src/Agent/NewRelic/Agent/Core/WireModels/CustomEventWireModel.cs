// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Core.JsonConverters;
using NewRelic.Agent.Core.Attributes;
using NewRelic.Agent.Extensions.Collections;
using Newtonsoft.Json;
using System;

namespace NewRelic.Agent.Core.WireModels
{
    public class CustomEventWireModel : IHasPriority, IEventWireModel
    {
        public IAttributeValueCollection AttributeValues { get; private set; }

        private float _priority;

        [JsonIgnore]
        public float Priority
        {
            get { return _priority; }
            set
            {
                const float priorityMin = 0.0f;
                if (value < priorityMin || float.IsNaN(value) || float.IsNegativeInfinity(value) || float.IsPositiveInfinity(value))
                {
                    throw new ArgumentException($"Custom event requires a valid priority value greater than {priorityMin}, value used: {value}");
                }
                _priority = value;
            }
        }

        public CustomEventWireModel(float priority, IAttributeValueCollection attribValues)
        {
            Priority = priority;
            AttributeValues = attribValues; 
            AttributeValues.MakeImmutable();
        }
    }
}
