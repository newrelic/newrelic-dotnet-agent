using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NewRelic.Agent.TestUtilities
{
	public static class AttributeComparer
	{
		public static void CompareDictionaries(IDictionary<string, object>[] expectedSerialization, IDictionary<string, object>[] actualserialization)
		{
			Assert.That(actualserialization.Length, Is.EqualTo(expectedSerialization.Length));

			for (var i = 0; i < expectedSerialization.Length; i++)
			{
				var actualDic = actualserialization[i];
				var expectedDic = expectedSerialization[i];

				try
				{
					CompareDictionary(expectedDic, actualDic);
				}
				catch (Exception ex)
				{
					throw new Exception($"Dictionary Comparison failed for dictionary {i}", ex);
				}
			}
		}

		public static void CompareDictionary(IDictionary<string, object> expectedDic, IDictionary<string, object> actualDic)
		{
			Assert.That((expectedDic != null && actualDic != null) || (expectedDic == null && actualDic == null));
			Assert.That(expectedDic.Count, Is.EqualTo(actualDic.Count));

			if (expectedDic == null || actualDic == null)
			{
				return;
			}

			foreach (var expectedKVP in expectedDic)
			{
				Assert.IsTrue(actualDic.ContainsKey(expectedKVP.Key));
				Assert.IsTrue(IsEqualTo(expectedKVP.Value, actualDic[expectedKVP.Key]), $"expected {expectedKVP.Key}-{expectedKVP.Value}, actual {actualDic[expectedKVP.Key]}");
			}
		}

		public static void DoesNotHaveKeys(IDictionary<string,object> actualDic, params string[] shouldNotHaveKeys)
		{
			var actualKeys = actualDic.Keys.ToList();
			var overlap = actualKeys.Intersect(shouldNotHaveKeys).ToArray();

			Assert.IsEmpty(overlap, $"The following keys should not exist in the dictionary: {string.Join(", ", overlap)}");
		}

		public static bool IsEqualTo(this object val1, object val2)
		{
			if (val1 == null && val2 == null) return true;
			if (val1 != null && val2 == null) return false;
			if (val1 == null && val2 != null) return false;

			//Will return consistent types
			var val1Comp = ConvertForCompare(val1);
			var val2Comp = ConvertForCompare(val2);

			switch (Type.GetTypeCode(val1Comp.GetType()))
			{
				case TypeCode.Single:
				case TypeCode.Double:
				case TypeCode.Decimal:
					return Math.Abs((double)val1Comp - (double)val2Comp) < .0001;

				default:
					return val1Comp.Equals(val2Comp);
			}
		}

		public static bool IsNotEqualTo(this object val1, object val2)
		{
			return !IsEqualTo(val1, val2);
		}

		public static object ConvertForCompare(object val)
		{
			if (val == null)
			{
				return null;
			}

			if (val is TimeSpan)
			{
				return ((TimeSpan)val).TotalSeconds;
			}

			if (val is DateTimeOffset)
			{
				return ((DateTimeOffset)val).ToString("o");
			}

			switch (Type.GetTypeCode(val.GetType()))
			{
				case TypeCode.SByte:
				case TypeCode.Byte:
				case TypeCode.Int16:
				case TypeCode.UInt16:
				case TypeCode.Int32:
				case TypeCode.UInt32:
				case TypeCode.Int64:
				case TypeCode.UInt64:
					return Convert.ToInt64(val);

				case TypeCode.Single:
				case TypeCode.Double:
				case TypeCode.Decimal:
					return Convert.ToDouble(val);

				case TypeCode.DateTime:
					return ((DateTime)val).ToString("o");

				case TypeCode.String:
					return val;

				default:
					return val.ToString();
			}
		}

	}
}