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
		private readonly List<Attribute> _agentAttributes = new List<Attribute>();
		[NotNull]
		private readonly List<Attribute> _userAttributes = new List<Attribute>();
		[NotNull]
		private readonly List<Attribute> _intrinsics = new List<Attribute>(); 

		public virtual int Count()
		{
			return _agentAttributes.Count + _userAttributes.Count + _intrinsics.Count;
		}

		[NotNull]
		public virtual IDictionary<string, object> GetAgentAttributesDictionary()
		{
			return _agentAttributes
				.Where(attribute => attribute != null)
				.Select(attribute => new KeyValuePair<string, object>(attribute.Key, attribute.Value))
				.ToDictionary(IEnumerableExtensions.DuplicateKeyBehavior.KeepFirst);
		}

		[NotNull]
		public virtual IDictionary<string, object> GetUserAttributesDictionary()
		{
			return _userAttributes
				.Where(attribute => attribute != null)
				.Select(attribute => new KeyValuePair<string, object>(attribute.Key, attribute.Value))
				.ToDictionary(IEnumerableExtensions.DuplicateKeyBehavior.KeepFirst);
		}

		[NotNull]
		public virtual IDictionary<string, object> GetIntrinsicsDictionary()
		{
			return _intrinsics
				.Where(attribute => attribute != null)
				.Select(attribute => new KeyValuePair<string, object>(attribute.Key, attribute.Value))
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
			GetListForAttributeClassification(attribute.Classification).Add(attribute);
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

		private List<Attribute> GetListForAttributeClassification([NotNull] AttributeClassification classification)
		{
			switch (classification)
			{
				case AttributeClassification.AgentAttributes:
					return _agentAttributes;
				case AttributeClassification.UserAttributes:
					return _userAttributes;
				case AttributeClassification.Intrinsics:
					return _intrinsics;
				default:
					return new List<Attribute>();
			}
		}
	}
}
