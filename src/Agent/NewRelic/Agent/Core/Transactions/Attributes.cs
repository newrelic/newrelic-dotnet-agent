// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using MoreLinq;
using NewRelic.SystemExtensions.Collections.Generic;

namespace NewRelic.Agent.Core.Transactions
{
    public class Attributes
    {
        private readonly IList<Attribute> _agentAttributes = new List<Attribute>();
        private readonly IList<Attribute> _userAttributes = new List<Attribute>();
        private readonly IList<Attribute> _intrinsics = new List<Attribute>();

        public virtual int Count()
        {
            return _agentAttributes.Count + _userAttributes.Count + _intrinsics.Count;
        }
        public virtual IDictionary<string, object> GetAgentAttributesDictionary()
        {
            return _agentAttributes
                .Where(attribute => attribute != null)
                .Select(attribute => new KeyValuePair<string, object>(attribute.Key, attribute.Value))
                .ToDictionary(IEnumerableExtensions.DuplicateKeyBehavior.KeepFirst);
        }
        public virtual IDictionary<string, object> GetUserAttributesDictionary()
        {
            return _userAttributes
                .Where(attribute => attribute != null)
                .Select(attribute => new KeyValuePair<string, object>(attribute.Key, attribute.Value))
                .ToDictionary(IEnumerableExtensions.DuplicateKeyBehavior.KeepFirst);
        }
        public virtual IDictionary<string, object> GetIntrinsicsDictionary()
        {
            return _intrinsics
                .Where(attribute => attribute != null)
                .Select(attribute => new KeyValuePair<string, object>(attribute.Key, attribute.Value))
                .ToDictionary(IEnumerableExtensions.DuplicateKeyBehavior.KeepFirst);
        }
        public virtual IList<Attribute> GetAgentAttributes()
        {
            return _agentAttributes;
        }
        public virtual IList<Attribute> GetUserAttributes()
        {
            return _userAttributes;
        }
        public virtual IList<Attribute> GetIntrinsics()
        {
            return _intrinsics;
        }

        public virtual void Add(Attribute attribute)
        {
            switch (attribute.Classification)
            {
                case AttributeClassification.AgentAttributes:
                    _agentAttributes.Add(attribute);
                    break;
                case AttributeClassification.UserAttributes:
                    _userAttributes.Add(attribute);
                    break;
                case AttributeClassification.Intrinsics:
                    _intrinsics.Add(attribute);
                    break;
            }
        }

        public virtual void Add(IEnumerable<Attribute> attributes)
        {
            attributes.Where(attribute => attribute != null).ForEach(Add);
        }

        public virtual void Add(Attributes attributes)
        {
            attributes._agentAttributes.ForEach(_agentAttributes.Add);
            attributes._intrinsics.ForEach(_intrinsics.Add);
            attributes._userAttributes.ForEach(_userAttributes.Add);
        }

        public virtual void TryAdd<T>(Func<T, Attribute> attributeBuilder, T value)
        {
            if (value == null)
                return;
            var attribute = attributeBuilder(value);
            Add(attribute);
        }

        public virtual void TryAdd<T>(Func<T, Attribute> attributeBuilder, T? value) where T : struct
        {
            if (value == null)
                return;
            var attribute = attributeBuilder(value.Value);
            Add(attribute);
        }

        public virtual void TryAddAll<T>(Func<T, IEnumerable<Attribute>> attributeBuilder, T value)
        {
            if (value == null)
                return;
            var attribute = attributeBuilder(value);
            Add(attribute);
        }

        public virtual void TryAddAll<T>(Func<T, IEnumerable<Attribute>> attributeBuilder, T? value) where T : struct
        {
            if (value == null)
                return;
            var attribute = attributeBuilder(value.Value);
            Add(attribute);
        }
    }
}
