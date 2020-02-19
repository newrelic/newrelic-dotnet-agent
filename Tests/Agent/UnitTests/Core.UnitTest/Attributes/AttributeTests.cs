using NUnit.Framework;
using NewRelic.Testing.Assertions;
using System;

namespace NewRelic.Agent.Core.Attributes.Tests
{
	[TestFixture]
	public class AttributeTests
	{
		[TestCase("stringValue", "stringValue")]
		[TestCase(true, true)]
		[TestCase((sbyte)1, 1L)]
		[TestCase((byte)2, 2L)]
		[TestCase((short)3, 3L)]
		[TestCase((ushort)4, 4L)]
		[TestCase(/*(int)*/5, 5L)]
		[TestCase((uint)6, 6L)]
		[TestCase((long)7, 7L)]
		[TestCase((ulong)8, 8L)]
		[TestCase((float)1.0, 1D)]
		[TestCase(/*(double)*/2.0, 2D)]
		public void Attributes_with_valid_type_are_valid_attributes(object attributeValue, object expectedResult)
		{
			var attribute = Attribute.BuildCustomAttribute("key", attributeValue);

			NrAssert.Multiple(
				() => Assert.IsTrue(attribute.IsValid),
				() => Assert.AreEqual(expectedResult, attribute.Value)
			);
		}

		[Test]
		public void Attributes_with_decimal_type_are_valid_attributes()
		{
			var testValue = 1.0m;
			var expectedValue = 1D;
			var attribute = Attribute.BuildCustomAttribute("key", testValue);

			NrAssert.Multiple(
				() => Assert.IsTrue(attribute.IsValid),
				() => Assert.AreEqual(expectedValue, attribute.Value)
			);
		}

		[Test]
		public void Attributes_with_DateTime_type_are_valid_attributes()
		{
			var testValue = DateTime.Now;
			var expectedValue = testValue.ToString("o");
			var attribute = Attribute.BuildCustomAttribute("key", testValue);

			NrAssert.Multiple(
				() => Assert.IsTrue(attribute.IsValid),
				() => Assert.AreEqual(expectedValue, attribute.Value)
			);
		}

		[Test]
		public void Attributes_with_TimeSpan_type_are_valid_attributes()
		{
			var testValue = TimeSpan.FromMilliseconds(1234);
			var expectedValue = 1.234d;
			var attribute = Attribute.BuildCustomAttribute("key", testValue);

			NrAssert.Multiple(
				() => Assert.IsTrue(attribute.IsValid),
				() => Assert.AreEqual(expectedValue, attribute.Value)
			);
		}

		[Test]
		public void Attributes_with_null_values_are_invalid_attributes()
		{
			var attribute = Attribute.BuildCustomAttribute("key", null);

			NrAssert.Multiple(
				() => Assert.IsFalse(attribute.IsValid),
				() => Assert.IsTrue(attribute.IsInvalidValueNull),
				() => Assert.AreEqual(null, attribute.Value)
			);
		}

		[Test]
		public void Attributes_with_empty_values_are_valid_attributes()
		{
			var attribute = Attribute.BuildCustomAttribute("key", string.Empty);

			NrAssert.Multiple(
				() => Assert.IsTrue(attribute.IsValid),
				() => Assert.AreEqual(string.Empty, attribute.Value)
			);
		}

		[Test]
		public void Attributes_with_blank_values_are_valid_attributes()
		{
			var attribute = Attribute.BuildCustomAttribute("key", " ");

			NrAssert.Multiple(
				() => Assert.IsTrue(attribute.IsValid),
				() => Assert.AreEqual(" ", attribute.Value)
			);
		}

		[Test]
		public void Attributes_key_size()
		{
			var key1 = new string('x', 255);
			var key2 = new string('a', 256);
			var key3 = string.Empty;
			var key4 = " ";
			var key5 = null as string;

			var validAttrib1 = Attribute.BuildCustomAttribute(key1, "Test Value");
			var invalidAttrib2 = Attribute.BuildCustomAttribute(key2, 9);
			var invalidAttrib3 = Attribute.BuildCustomAttribute(key3, 8.3);
			var invalidAttrib4 = Attribute.BuildCustomAttribute(key4, true);
			var invalidAttrib5 = Attribute.BuildCustomAttribute(key5, true);

			NrAssert.Multiple(
				() => Assert.IsTrue(validAttrib1.IsValid),
				
				() => Assert.IsFalse(invalidAttrib2.IsValid),
				() => Assert.IsTrue(invalidAttrib2.IsInvalidKeyTooLarge),
				
				() => Assert.IsFalse(invalidAttrib3.IsValid),
				() => Assert.IsTrue(invalidAttrib3.IsInvalidKeyEmpty),

				() => Assert.IsFalse(invalidAttrib4.IsValid),
				() => Assert.IsTrue(invalidAttrib4.IsInvalidKeyEmpty),

				() => Assert.IsFalse(invalidAttrib5.IsValid),
				() => Assert.IsTrue(invalidAttrib5.IsInvalidKeyEmpty)

			);
		}
	}
}
