// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using System.Collections.Generic;
using System;
using System.Collections.ObjectModel;
using NewRelic.Core.Logging;
using System.Linq;
using System.Threading;

namespace NewRelic.Agent.Core.Attributes
{
    public interface IAttributeValueCollection
    {
        /// <summary>
        /// Even this enum is Flags, this should only be one attribute destination
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

        IDictionary<string, object> GetAllAttributeValuesDic();
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

        // In priority order: user < intrinsics < agent
        protected static readonly AttributeClassification[] _allClassifications = new[] { AttributeClassification.UserAttributes, AttributeClassification.Intrinsics, AttributeClassification.AgentAttributes };

        private int _intrinsicAttributeCount;
        private int _agentAttributeCount;
        private int _userAttributeCount;

        private readonly string _transactionGuid;

        public int Count => _intrinsicAttributeCount + _agentAttributeCount + _userAttributeCount;

        public AttributeDestinations[] TargetModelTypes { get; private set; }
        public AttributeDestinations TargetModelTypesAsFlags { get; private set; }

        public abstract bool CollectionContainsAttribute(AttributeDefinition attrDef);

        private bool ValidateCollectionLimits(AttributeDefinition attrDef)
        {
            var attributeLimitReached = false;
            string message = string.Empty;

            if (attrDef.Classification == AttributeClassification.UserAttributes && _userAttributeCount >= MaxCountUserAttrib)
            {
                message = $"User Attribute '{attrDef.Name}' was not recorded - A max of {MaxCountUserAttrib} User Attributes may be supplied.";
                attributeLimitReached = true;
            }
            else if (Count >= MaxCountAllAttrib)
            {
                message = $"{attrDef.Classification} Attribute '{attrDef.Name}' was not recorded - A max of {MaxCountAllAttrib} attributes may be supplied.";
                attributeLimitReached = true;
            }

            // Log a message and return false if we hit an attribute limit but don't currently contain the attribute
            if (attributeLimitReached && !CollectionContainsAttribute(attrDef))
            {
                LogTransactionIfFinest(message);
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

        protected AttributeValueCollectionBase(string transactionGuid, params AttributeDestinations[] targetModelTypes)
        {
            TargetModelTypes = targetModelTypes;
            foreach (var targetModelType in targetModelTypes)
            {
                TargetModelTypesAsFlags |= targetModelType;
            }

            _transactionGuid = transactionGuid;
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

        public IDictionary<string, object> GetAllAttributeValuesDic()
        {
            var result = new Dictionary<string, object>();
            foreach (var classification in _allClassifications)
            {
                foreach (var attribVal in GetAttribValuesImpl(classification))
                {
                    // This will overwrite existing values in the order of: user < intrinsic < agent
                    result[attribVal.AttributeDefinition.Name] = attribVal.Value;
                }
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
                    Interlocked.Increment(ref _intrinsicAttributeCount);
                    break;

                case AttributeClassification.AgentAttributes:
                    Interlocked.Increment(ref _agentAttributeCount);
                    break;

                case AttributeClassification.UserAttributes:
                    Interlocked.Increment(ref _userAttributeCount);
                    break;

            }
        }

        public bool TrySetValue<TInput, TOutput>(AttributeDefinition<TInput, TOutput> attribDef, TOutput val)
        {
            if (!ValidateCollectionLimits(attribDef))
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
            if (!ValidateCollectionLimits(attribValue.AttributeDefinition))
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
            if (lazyValueImpl == null || !ValidateCollectionLimits(attribDef))
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
                        Log.Finest(ex, "{attribVal.AttributeDefinition.Classification} Attribute '{attribVal.AttributeDefinition.Name}' was not recorded - exception occurred while resolving value (lazy)");
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

        private void LogTransactionIfFinest(string message)
        {
            if (Log.IsFinestEnabled && !string.IsNullOrWhiteSpace(_transactionGuid))
            {
                Log.Finest($"Trx {_transactionGuid}: {message}");
            }
            else
            {
                Log.Debug(message);
            }
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

        public AttributeValueCollection(string transactionGuid, params AttributeDestinations[] targetModelTypes) : base(transactionGuid, targetModelTypes)
        {
        }

        private readonly Dictionary<AttributeClassification, object> _lockObjects = new Dictionary<AttributeClassification, object>
        {
            { AttributeClassification.AgentAttributes, new object() },
            { AttributeClassification.Intrinsics, new object() },
            { AttributeClassification.UserAttributes, new object() }
        };

        private Dictionary<Guid, AttributeValue> _intrinsicAttributes;
        private Dictionary<Guid, AttributeValue> _agentAttributes;
        private Dictionary<Guid, AttributeValue> _userAttributes;

        private Dictionary<Guid, AttributeValue> GetAttribValuesInternal(AttributeClassification classification, bool withCreate)
        {
            switch (classification)
            {
                case AttributeClassification.Intrinsics:
                    return withCreate
                        ? _intrinsicAttributes ??= new Dictionary<Guid, AttributeValue>()
                        : _intrinsicAttributes;

                case AttributeClassification.AgentAttributes:
                    return withCreate
                        ? _agentAttributes ??= new Dictionary<Guid, AttributeValue>()
                        : _agentAttributes;

                case AttributeClassification.UserAttributes:
                    return withCreate
                        ? _userAttributes ??= new Dictionary<Guid, AttributeValue>()
                        : _userAttributes;
            }

            return null;
        }

        protected override IEnumerable<AttributeValue> GetAttribValuesImpl(AttributeClassification classification)
        {
            var dic = GetAttribValuesInternal(classification, false);

            return dic?.Values ?? Enumerable.Empty<AttributeValue>();
        }

        protected override void RemoveItemsImpl(IEnumerable<AttributeValue> itemsToRemove)
        {
            foreach (var lockObjKvp in _lockObjects)
            {
                var guidsToRemove = itemsToRemove
                    .Where(x => x.AttributeDefinition.Classification == lockObjKvp.Key)
                    .Select(x => x.AttributeDefinition.Guid)
                    .ToArray();

                if (guidsToRemove.Length == 0)
                {
                    continue;
                }

                var dicForClassification = GetAttribValuesInternal(lockObjKvp.Key, false);
                if (dicForClassification == null)
                {
                    continue;
                }

                lock (lockObjKvp.Value)
                {
                    foreach (var guidToRemove in guidsToRemove)
                    {
                        dicForClassification.Remove(guidToRemove);
                    }
                }
            }
        }

        public override bool CollectionContainsAttribute(AttributeDefinition attrDef)
        {
            var lockObj = _lockObjects[attrDef.Classification];
            lock (lockObj)
            {
                var dic = GetAttribValuesInternal(attrDef.Classification, false);
                var hasItem = dic?.ContainsKey(attrDef.Guid);
                return hasItem.HasValue && hasItem.Value;
            }
        }

        protected override bool SetValueImpl(IAttributeValue attribVal)
        {
            if (attribVal is AttributeValue attribValTyped)
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

            var attribVal = new AttributeValue(attribDef)
            {
                Value = value
            };

            return SetValueImplInternal(attribVal);
        }

        protected override bool SetValueImpl(AttributeDefinition attribDef, Lazy<object> lazyValue)
        {
            if (IsImmutable)
            {
                return false;
            }

            var attribVal = new AttributeValue(attribDef)
            {
                LazyValue = lazyValue
            };

            return SetValueImplInternal(attribVal);
        }

        private bool SetValueImplInternal(AttributeValue attribVal)
        {
            if (IsImmutable)
            {
                return false;
            }

            var lockObj = _lockObjects[attribVal.AttributeDefinition.Classification];
            lock (lockObj)
            {
                var dic = GetAttribValuesInternal(attribVal.AttributeDefinition.Classification, true);
                var hasItem = dic.ContainsKey(attribVal.AttributeDefinition.Guid);
                dic[attribVal.AttributeDefinition.Guid] = attribVal;

                return !hasItem;
            }
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

        public IDictionary<string, object> GetAllAttributeValuesDic()
        {
            return _emptyAttribDic;
        }
    }
}
