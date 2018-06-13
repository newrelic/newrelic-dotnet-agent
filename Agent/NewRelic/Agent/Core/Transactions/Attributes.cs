using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using NewRelic.SystemExtensions.Collections.Generic;

namespace NewRelic.Agent.Core.Transactions
{
	public class Attributes
	{
		public const int UserAttributeClamp = 64;

		[NotNull]
		private readonly IList<Attribute> _agentAttributes = new List<Attribute>();
		[NotNull]
		private readonly IList<Attribute> _userAttributes = new List<Attribute>();
		[NotNull]
		private readonly IList<Attribute> _intrinsics = new List<Attribute>(); 

		public virtual int Count()
		{
			return _agentAttributes.Count + _userAttributes.Count + _intrinsics.Count;
		}

		[NotNull]
		public virtual IDictionary<String, Object> GetAgentAttributesDictionary()
		{
			return _agentAttributes
				.Where(attribute => attribute != null)
				.Select(attribute => new KeyValuePair<String, Object>(attribute.Key, attribute.Value))
				.ToDictionary(IEnumerableExtensions.DuplicateKeyBehavior.KeepFirst);
		}

		[NotNull]
		public virtual IDictionary<String, Object> GetUserAttributesDictionary()
		{
			return _userAttributes
				.Where(attribute => attribute != null)
				.Select(attribute => new KeyValuePair<String, Object>(attribute.Key, attribute.Value))
				.ToDictionary(IEnumerableExtensions.DuplicateKeyBehavior.KeepFirst);
		}

		[NotNull]
		public virtual IDictionary<String, Object> GetIntrinsicsDictionary()
		{
			return _intrinsics
				.Where(attribute => attribute != null)
				.Select(attribute => new KeyValuePair<String, Object>(attribute.Key, attribute.Value))
				.ToDictionary(IEnumerableExtensions.DuplicateKeyBehavior.KeepFirst);
		}

		[NotNull]
		public virtual IList<Attribute> GetAgentAttributes()
		{
			return _agentAttributes;
		}

		[NotNull]
		public virtual IList<Attribute> GetUserAttributes()
		{
			return _userAttributes;
		}

		[NotNull]
		public virtual IList<Attribute> GetIntrinsics()
		{
			return _intrinsics;
		}

		public virtual void Add([NotNull] Attribute attribute)
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

		public virtual void Add([NotNull] IEnumerable<Attribute> attributes)
		{
			foreach (var attr in attributes)
			{
				if ( attr != null)
				{
					Add(attr);
				}
			}
		}

		public virtual void TryAdd<T>([NotNull] Func<T, Attribute> attributeBuilder, [CanBeNull] T value)
		{
			if (value == null)
				return;
			var attribute = attributeBuilder(value);
			Add(attribute);
		}

		public virtual void TryAdd<T>([NotNull] Func<T, Attribute> attributeBuilder, [CanBeNull] T? value) where T : struct
		{
			if (value == null)
				return;
			var attribute = attributeBuilder(value.Value);
			Add(attribute);
		}

		public virtual void TryAddAll<T>([NotNull] Func<T, IEnumerable<Attribute>> attributeBuilder, [CanBeNull] T value)
		{
			if (value == null)
				return;
			var attribute = attributeBuilder(value);
			Add(attribute);
		}

		public virtual void TryAddAll<T>([NotNull] Func<T, IEnumerable<Attribute>> attributeBuilder, [CanBeNull] T? value) where T : struct
		{
			if (value == null)
				return;
			var attribute = attributeBuilder(value.Value);
			Add(attribute);
		}
	}
}
