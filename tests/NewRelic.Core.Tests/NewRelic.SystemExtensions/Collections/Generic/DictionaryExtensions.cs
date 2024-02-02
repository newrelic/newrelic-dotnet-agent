// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#if NETFRAMEWORK
using System;
using System.Collections.Generic;
using NewRelic.SystemExtensions.Collections.Generic;
using NUnit.Framework;


namespace NewRelic.SystemExtensions.UnitTests.Collections.Generic
{
	public class DictionaryExtensions
	{
		public class GetValueOrDefault
		{
			[Test]
			public void when_no_default_provided_and_key_exists_then_value_from_dictionary_is_returned()
				{
					const string expectedKey = "foo";
					const string expectedValue = "bar";
					var dictionary = new Dictionary<string, string> {{expectedKey, expectedValue}};

					var actualValue = dictionary.GetValueOrDefault(expectedKey);

                Assert.That(actualValue, Is.EqualTo(expectedValue));
				}

			[Test]
			public void when_no_default_provided_for_reference_type_and_key_does_not_exist_then_null_is_returned()
				{
					const string expectedKey = "foo";
					const string expectedValue = null;
					var dictionary = new Dictionary<string, string>();

					var actualValue = dictionary.GetValueOrDefault(expectedKey);

                Assert.That(actualValue, Is.EqualTo(expectedValue));
				}

			[Test]
			public void when_no_default_provided_for_value_type_and_key_does_not_exist_then_0_is_returned()
				{
					const string expectedKey = "foo";
					const int expectedValue = 0;
					var dictionary = new Dictionary<string, int>();

					var actualValue = dictionary.GetValueOrDefault(expectedKey);

                Assert.That(actualValue, Is.EqualTo(expectedValue));
				}

			[Test]
			public void when_default_provided_and_key_exists_then_value_from_dictionary_is_returned()
			{
				const string expectedKey = "foo";
				const string expectedValue = "bar";
				var dictionary = new Dictionary<string, string> { { expectedKey, expectedValue } };

				var actualValue = dictionary.GetValueOrDefault(expectedKey, "default");

                Assert.That(actualValue, Is.EqualTo(expectedValue));
			}

			[Test]
			public void when_default_provided_for_reference_type_and_key_does_not_exist_then_default_is_returned()
			{
				const string expectedKey = "foo";
				const string expectedValue = "default";
				var dictionary = new Dictionary<string, string>();

				var actualValue = dictionary.GetValueOrDefault(expectedKey, expectedValue);

                Assert.That(actualValue, Is.EqualTo(expectedValue));
			}

			[Test]
			public void when_default_provided_for_value_type_and_key_does_not_exist_then_default_is_returned()
			{
				const string expectedKey = "foo";
				const int expectedValue = 123;
				var dictionary = new Dictionary<string, int>();

				var actualValue = dictionary.GetValueOrDefault(expectedKey, expectedValue);

                Assert.That(actualValue, Is.EqualTo(expectedValue));
			}

			[Test]
			public void when_default_function_provided_and_key_exists_then_value_from_dictionary_is_returned()
			{
				const string expectedKey = "foo";
				const string expectedValue = "bar";
				var dictionary = new Dictionary<string, string> { { expectedKey, expectedValue } };

				var actualValue = dictionary.GetValueOrDefault(expectedKey, () => "default");

                Assert.That(actualValue, Is.EqualTo(expectedValue));
			}

			[Test]
			public void when_default_function_provided_for_reference_type_and_key_does_not_exist_then_default_is_returned()
			{
				const string expectedKey = "foo";
				const string expectedValue = "default";
				var dictionary = new Dictionary<string, string>();

				var actualValue = dictionary.GetValueOrDefault(expectedKey, () => expectedValue);

                Assert.That(actualValue, Is.EqualTo(expectedValue));
			}

			[Test]
			public void when_default_function_provided_for_value_type_and_key_does_not_exist_then_default_is_returned()
			{
				const string expectedKey = "foo";
				const int expectedValue = 123;
				var dictionary = new Dictionary<string, int>();

				var actualValue = dictionary.GetValueOrDefault(expectedKey, () => expectedValue);

                Assert.That(actualValue, Is.EqualTo(expectedValue));
			}

			[Test]
			public void when_dictionary_is_null_and_no_default_is_provided_then_null_is_returned()
			{
				const string expectedKey = "foo";
				const string expectedValue = null;
				var dictionary = null as IDictionary<string, string>;

				var actualValue = dictionary.GetValueOrDefault(expectedKey);

                Assert.That(actualValue, Is.EqualTo(expectedValue));
			}

			[Test]
			public void when_dictionary_is_null_and_default_is_provided_then_default_is_returned()
			{
				const string expectedKey = "foo";
				const string expectedValue = "default";
				var dictionary = null as IDictionary<string, string>;

				var actualValue = dictionary.GetValueOrDefault(expectedKey, expectedValue);

                Assert.That(actualValue, Is.EqualTo(expectedValue));
			}

			[Test]
			public void when_dictionary_is_null_and_default_function_is_provided_then_default_is_returned()
			{
				const string expectedKey = "foo";
				const string expectedValue = "default";
				var dictionary = null as IDictionary<string, string>;

				var actualValue = dictionary.GetValueOrDefault(expectedKey, () => expectedValue);

                Assert.That(actualValue, Is.EqualTo(expectedValue));
			}

			[Test]
			public void when_key_is_null_and_no_default_is_provided_then_null_is_returned()
			{
				const string expectedKey = null;
				const string expectedValue = null;
				var dictionary = new Dictionary<string, string>();

				var actualValue = dictionary.GetValueOrDefault(expectedKey);

                Assert.That(actualValue, Is.EqualTo(expectedValue));
			}

			[Test]
			public void when_key_is_null_and_default_is_provided_then_default_is_returned()
			{
				const string expectedKey = null;
				const string expectedValue = "default";
				var dictionary = new Dictionary<string, string>();

				var actualValue = dictionary.GetValueOrDefault(expectedKey, expectedValue);

                Assert.That(actualValue, Is.EqualTo(expectedValue));
			}

			[Test]
			public void when_key_is_null_and_default_function_is_provided_then_default_is_returned()
			{
				const string expectedKey = null;
				const string expectedValue = "default";
				var dictionary = new Dictionary<string, string>();

				var actualValue = dictionary.GetValueOrDefault(expectedKey, () => expectedValue);

                Assert.That(actualValue, Is.EqualTo(expectedValue));
			}

			[Test]
			public void when_defaultEvaluator_is_null_then_ArgumentNullException_is_thrown()
			{
				const string expectedKey = "foo";
				const string expectedValue = "bar";
				var dictionary = new Dictionary<string, string> {{expectedKey, expectedValue}};

				Assert.Throws<ArgumentNullException>(() => dictionary.GetValueOrDefault(expectedKey, null as Func<string>));
			}

		}
	}
}
#endif
