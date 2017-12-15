using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using NewRelic.Collections;

namespace NewRelic.Agent
{
	public class LazyMap<TKey, TValue> : ILazyMap<TKey, TValue>
	{
		// starts out empty, will eventually be populated by every instrumented method
		[NotNull]
		private readonly ConcurrentDictionary<TKey, Maybe> _dictionary = new ConcurrentDictionary<TKey, Maybe>();

		[NotNull]
		private readonly IEnumerable<TValue> _values;

		[NotNull]
		private readonly Func<TKey, TValue, Boolean> _keyValueResolver;

		public LazyMap([CanBeNull] IEnumerable<TValue> values, [NotNull] Func<TKey, TValue, Boolean> keyValueResolver)
		{
			// we make a copy of this list because we need to be assured that it isn't modified later
			_values = (values ?? Enumerable.Empty<TValue>()).ToList();
			_keyValueResolver = keyValueResolver;
		}

		public TValue Get(TKey key)
		{
			if (key == null)
				return default(TValue);

			var wrapperResult = _dictionary.GetOrSetValue(key, () => FindValue(key));
			return wrapperResult.Value;
		}

		public void Override(TKey key, TValue value)
		{
			_dictionary[key] = new Maybe(value);
		}

		[NotNull]
		private Maybe FindValue(TKey key)
		{
			var value = _values
				.Where(possibleValue => possibleValue != null)
				.Where(possibleValue => _keyValueResolver(key, possibleValue))
				.FirstOrDefault();

			if (value == null)
				return Maybe.None;

			return new Maybe(value);
		}

		/// <summary>
		/// This is necessary because ConcurrentDictionary.GetOrSetValue does not work with null values, so we have to create an option type that we can store in it.
		/// </summary>
		private class Maybe
		{
			// ReSharper disable once StaticFieldInGenericType
			[NotNull]
			public static readonly Maybe None = new Maybe();

			[CanBeNull]
			public readonly TValue Value;

			public Maybe([NotNull] TValue value)
			{
				Value = value;
			}

			private Maybe()
			{
				Value = default(TValue);
			}
		}
	}
}
