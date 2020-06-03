using NewRelic.Agent.Core.Attributes;
using NewRelic.Agent.Core.JsonConverters;
using NewRelic.Collections;
using Newtonsoft.Json;
using System;

namespace NewRelic.Agent.Core.WireModels
{
    [JsonConverter(typeof(EventWireModelSerializer))]
    public interface IEventWireModel
    {
        IAttributeValueCollection AttributeValues { get; }
    }

    public abstract class EventWireModel : IHasPriority, IEventWireModel
    {
        public IAttributeValueCollection AttributeValues { get; private set; }

        private readonly AttributeDestinations _targetObject;

        public readonly bool IsSynthetics;

        private float _priority;
        public float Priority
        {
            get { return _priority; }
            set
            {
                const float priorityMin = 0.0f;
                if (value < priorityMin || float.IsNaN(value) || float.IsNegativeInfinity(value) || float.IsPositiveInfinity(value))
                {
                    throw new ArgumentException($"{_targetObject} requires a valid priority value greater than {priorityMin}, value used: {value}");
                }
                _priority = value;
            }
        }

        protected EventWireModel(AttributeDestinations targetObject, IAttributeValueCollection attribValues, bool isSynthetics, float priority)
        {
            _targetObject = targetObject;
            AttributeValues = new AttributeValueCollection(attribValues, _targetObject);
            Priority = priority;
            IsSynthetics = isSynthetics;

            AttributeValues.MakeImmutable();
        }
    }

    public class ErrorEventWireModel : EventWireModel
    {
        public ErrorEventWireModel(IAttributeValueCollection attribValues, bool isSynthetics, float priority)
            : base(AttributeDestinations.ErrorEvent, attribValues, isSynthetics, priority)
        {
        }
    }
}
