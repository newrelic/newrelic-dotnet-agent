// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using System.Collections.Generic;
using System;
using NewRelic.Core.Logging;
using System.Linq;
using System.Threading;

namespace NewRelic.Agent.Core.Attributes
{
    public interface IAttributeValueCollection
    {
        /// <summary>
        /// Even this enum is Flags, this should only be one attributedestination
        /// </summary>
        AttributeDestinations[] TargetModelTypes { get; }

        AttributeDestinations TargetModelTypesAsFlags { get; }

        int Count { get; }

        bool IsImmutable { get; }

        void AddRange(IAttributeValueCollection fromCollection);

        void MakeImmutable();

        bool TrySetValue<TInput, TOutput>(AttributeDefinition<TInput, TOutput> attribDef, TOutput value);

        bool TrySetValue<TInput, TOutput>(AttributeDefinition<TInput, TOutput> attribDef, Lazy<object> lazyImpl);

        bool TrySetValue(IAttributeValue attrib);

        IEnumerable<IAttributeValue> GetAttributeValues(AttributeClassification classification);

        IDictionary<string, object> GetAttributeValuesDic(AttributeClassification classification);
    }

    public abstract class AttributeValueCollectionBase<TAttrib> : IAttributeValueCollection where TAttrib : IAttributeValue
    {

        private static AttributeDestinations[] _allTargetModelTypes;

        public static AttributeDestinations[] AllTargetModelTypes => _allTargetModelTypes
            ?? (_allTargetModelTypes = Enum.GetValues(typeof(AttributeDestinations))
            .Cast<AttributeDestinations>()
            .Where(x => x != AttributeDestinations.All && x != AttributeDestinations.None)
            .ToArray());

        public const int MaxCountUserAttrib = 64;
        public const int MaxCountAllAttrib = 255;

        protected static readonly AttributeClassification[] _allClassifications = new[] { AttributeClassification.Intrinsics, AttributeClassification.AgentAttributes, AttributeClassification.UserAttributes };

        private int _attribCountIntrinsicAttribs;
        private int _attribCountAgentAttribs;
        private int _attribCountUserAttribs;

        public int Count => _attribCountIntrinsicAttribs + _attribCountAgentAttribs + _attribCountUserAttribs;

        public AttributeDestinations[] TargetModelTypes { get; private set; }
        public AttributeDestinations TargetModelTypesAsFlags { get; private set; }

        private bool ValidateCollectionLimits(AttributeClassification classification, string name)
        {
            int attribCount = 0;
            switch (classification)
            {
                case AttributeClassification.Intrinsics:
                    attribCount = _attribCountIntrinsicAttribs;
                    break;

                case AttributeClassification.AgentAttributes:
                    attribCount = _attribCountAgentAttribs;
                    break;

                case AttributeClassification.UserAttributes:
                    attribCount = _attribCountUserAttribs;
                    break;
            }

            if (classification == AttributeClassification.UserAttributes && attribCount >= MaxCountUserAttrib)
            {
                Log.Debug($"{classification} Attribute '{name}' was not recorded - A max of {MaxCountUserAttrib} {classification} attributes may be supplied.");
                return false;
            }

            if (Count >= MaxCountAllAttrib)
            {
                Log.Debug($"{classification} Attribute '{name}' was not recorded - A max of {MaxCountAllAttrib} attributes may be supplied.");
                return false;
            }

            return true;
        }

        private AttributeValueCollectionBase() { }

        protected AttributeValueCollectionBase(IAttributeValueCollection fromCollection, params AttributeDestinations[] targetModelTypes)
            : this(targetModelTypes)
        {
            AddRange(fromCollection);
        }

        protected AttributeValueCollectionBase(params AttributeDestinations[] targetModelTypes)
        {
            TargetModelTypes = targetModelTypes;
            foreach (var targetModelType in targetModelTypes)
            {
                TargetModelTypesAsFlags |= targetModelType;
            }
        }

        public IEnumerable<IAttributeValue> GetAttributeValues(AttributeClassification classification)
        {
            return GetAttribValuesImpl(classification).Cast<IAttributeValue>();
        }

        public IDictionary<string, object> GetAttributeValuesDic(AttributeClassification classification)
        {
            var result = new Dictionary<string, object>();
            foreach (var attribVal in GetAttribValuesImpl(classification))
            {
                result[attribVal.AttributeDefinition.Name] = attribVal.Value;
            }

            return result;
        }

        public void AddRange(IEnumerable<IAttributeValue> attribValues)
        {
            foreach (var attribVal in attribValues)
            {

                if ((attribVal.AttributeDefinition.AttributeDestinations & TargetModelTypesAsFlags) == 0)
                {
                    continue;
                }

                TrySetValue(attribVal);
            }
        }

        public void AddRange(IAttributeValueCollection fromCollection)
        {
            foreach (var classification in _allClassifications)
            {
                AddRange(fromCollection.GetAttributeValues(classification));
            }
        }

        public bool IsImmutable { get; private set; } = false;

        private void IncrementAttribCount(AttributeClassification classification)
        {
            switch (classification)
            {
                case AttributeClassification.Intrinsics:
                    Interlocked.Increment(ref _attribCountIntrinsicAttribs);
                    break;

                case AttributeClassification.AgentAttributes:
                    Interlocked.Increment(ref _attribCountAgentAttribs);
                    break;

                case AttributeClassification.UserAttributes:
                    Interlocked.Increment(ref _attribCountUserAttribs);
                    break;

            }
        }

        public bool TrySetValue<TInput, TOutput>(AttributeDefinition<TInput, TOutput> attribDef, TOutput val)
        {
            if (!ValidateCollectionLimits(attribDef.Classification, attribDef.Name))
            {
                return false;
            }

            if (!SetValueImpl(attribDef, val))
            {
                return false;
            }

            IncrementAttribCount(attribDef.Classification);

            return true;

        }

        public bool TrySetValue(IAttributeValue attribValue)
        {
            if (!ValidateCollectionLimits(attribValue.AttributeDefinition.Classification, attribValue.AttributeDefinition.Name))
            {
                return false;
            }

            if (!SetValueImpl(attribValue))
            {
                return false;
            }

            IncrementAttribCount(attribValue.AttributeDefinition.Classification);

            return true;

        }

        public bool TrySetValue<TInput, TOutput>(AttributeDefinition<TInput, TOutput> attribDef, Lazy<object> lazyValueImpl)
        {
            if (!ValidateCollectionLimits(attribDef.Classification, attribDef.Name) || lazyValueImpl == null)
            {
                return false;
            }

            if (!SetValueImpl(attribDef, lazyValueImpl))
            {
                return false;
            }


            IncrementAttribCount(attribDef.Classification);

            return true;
        }


        protected abstract bool SetValueImpl(IAttributeValue attribVal);

        protected abstract bool SetValueImpl(AttributeDefinition attribDef, object value);

        protected abstract bool SetValueImpl(AttributeDefinition attribDef, Lazy<object> lazyValue);

        protected abstract void RemoveItemsImpl(IEnumerable<TAttrib> itemsToRemove);

        protected abstract IEnumerable<TAttrib> GetAttribValuesImpl(AttributeClassification classification);

        public void MakeImmutable()
        {
            if (IsImmutable)
            {
                return;
            }

            foreach (var classification in _allClassifications)
            {
                var itemsToRemove = new List<TAttrib>();
                foreach (var attribVal in GetAttribValuesImpl(classification))
                {
                    try
                    {
                        attribVal.MakeImmutable();

                        if (attribVal.Value == null)
                        {
                            //Nothing to log here because the ResolveLazyValue function
                            //records the error message
                            itemsToRemove.Add(attribVal);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Finest($"{attribVal.AttributeDefinition.Classification} Attribute '{attribVal.AttributeDefinition.Name}' was not recorded - exception occurred while resolving value (lazy) - {ex}");
                        itemsToRemove.Add(attribVal);
                    }
                }

                if (itemsToRemove.Count > 0)
                {
                    RemoveItemsImpl(itemsToRemove);
                }
            }

            IsImmutable = true;
        }
    }

    public class AttributeValueCollection : AttributeValueCollectionBase<AttributeValue>
    {
        public AttributeValueCollection(IAttributeValueCollection fromCollection, params AttributeDestinations[] targetModelTypes) : base(fromCollection, targetModelTypes)
        {
        }

        public AttributeValueCollection(params AttributeDestinations[] targetModelTypes) : base(targetModelTypes)
        {
        }

        private List<AttributeValue> _attribValuesIntrinsicAttribs;
        private List<AttributeValue> _attribValuesAgentAttribs;
        private List<AttributeValue> _attribValuesUserAttribs;

        private List<AttributeValue> GetAttribValues(AttributeClassification classification, bool withCreate)
        {
            switch (classification)
            {
                case AttributeClassification.Intrinsics:
                    return withCreate
                        ? _attribValuesIntrinsicAttribs ?? (_attribValuesIntrinsicAttribs = new List<AttributeValue>())
                        : _attribValuesIntrinsicAttribs;

                case AttributeClassification.AgentAttributes:
                    return withCreate
                        ? _attribValuesAgentAttribs ?? (_attribValuesAgentAttribs = new List<AttributeValue>())
                        : _attribValuesAgentAttribs;

                case AttributeClassification.UserAttributes:
                    return withCreate
                        ? _attribValuesUserAttribs ?? (_attribValuesUserAttribs = new List<AttributeValue>())
                        : _attribValuesUserAttribs;
            }

            return null;
        }

        protected override IEnumerable<AttributeValue> GetAttribValuesImpl(AttributeClassification classification)
        {
            var dic = GetAttribValues(classification, false);

            return dic == null
                ? Enumerable.Empty<AttributeValue>()
                : dic;
        }

        protected override void RemoveItemsImpl(IEnumerable<AttributeValue> itemsToRemove)
        {

            if (_attribValuesIntrinsicAttribs != null)
            {
                Interlocked.Exchange(ref _attribValuesIntrinsicAttribs, new List<AttributeValue>(_attribValuesIntrinsicAttribs.Except(itemsToRemove)));
            }

            if (_attribValuesAgentAttribs != null)
            {
                Interlocked.Exchange(ref _attribValuesAgentAttribs, new List<AttributeValue>(_attribValuesAgentAttribs.Except(itemsToRemove)));
            }

            if (_attribValuesUserAttribs != null)
            {
                Interlocked.Exchange(ref _attribValuesUserAttribs, new List<AttributeValue>(_attribValuesUserAttribs.Except(itemsToRemove)));
            }
        }

        protected override bool SetValueImpl(IAttributeValue attribVal)
        {
            var attribValTyped = attribVal as AttributeValue;
            if (attribValTyped != null)
            {
                return SetValueImplInternal(attribValTyped);
            }

            if (attribVal.Value != null)
            {
                return SetValueImpl(attribVal.AttributeDefinition, attribVal.Value);
            }

            if (attribVal.LazyValue != null)
            {
                return SetValueImpl(attribVal.AttributeDefinition, attribVal.LazyValue);
            }

            return false;
        }

        protected override bool SetValueImpl(AttributeDefinition attribDef, object value)
        {
            if (IsImmutable)
            {
                return false;
            }

            var attribVal = new AttributeValue(attribDef);
            attribVal.Value = value;

            return SetValueImplInternal(attribVal);
        }

        protected override bool SetValueImpl(AttributeDefinition attribDef, Lazy<object> lazyValue)
        {
            if (IsImmutable)
            {
                return false;
            }

            var attribVal = new AttributeValue(attribDef);
            attribVal.LazyValue = lazyValue;

            return SetValueImplInternal(attribVal);
        }

        private object syncObj = new object();

        private bool SetValueImplInternal(AttributeValue attribVal)
        {
            if (IsImmutable)
            {
                return false;
            }

            lock (syncObj)
            {
                var dic = GetAttribValues(attribVal.AttributeDefinition.Classification, true);
                dic.Add(attribVal);
            }

            return true;
        }
    }

    public class NoOpAttributeValueCollection : IAttributeValueCollection
    {
        public AttributeDestinations[] TargetModelTypes { get; } = new[] { AttributeDestinations.None };
        public AttributeDestinations TargetModelTypesAsFlags => AttributeDestinations.None;

        public bool IsImmutable => false;

        public int Count => 0;

        private static IEnumerable<IAttributeValue> _emptyAttribValues = new List<IAttributeValue>();
        private static IDictionary<string, object> _emptyAttribDic = new ReadOnlyDictionary<string, object>(new Dictionary<string, object>());

        public void MakeImmutable()
        {
        }

        public IEnumerable<IAttributeValue> GetAttributeValues(AttributeClassification classification)
        {
            return _emptyAttribValues;
        }

        public void AddRange(IAttributeValueCollection fromCollection)
        {
            return;
        }

        public bool TrySetValue<TInput, TOutput>(AttributeDefinition<TInput, TOutput> attribDef, TOutput value)
        {
            return false;
        }

        public bool TrySetValue<TInput, TOutput>(AttributeDefinition<TInput, TOutput> attribDef, Lazy<object> lazyImpl)
        {
            return false;
        }

        public bool TrySetValue(IAttributeValue attrib)
        {
            return false;
        }

        public IDictionary<string, object> GetAttributeValuesDic(AttributeClassification classification)
        {
            return _emptyAttribDic;
        }
    }

}
