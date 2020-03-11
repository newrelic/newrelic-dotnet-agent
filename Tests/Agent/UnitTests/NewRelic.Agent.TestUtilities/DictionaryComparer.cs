using NUnit.Framework;
using System;
using System.Collections.Generic;

namespace NewRelic.Agent.TestUtilities
{
	public static class DictionaryComparer
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
				Assert.That(ConvertForCompare(expectedKVP.Value), Is.EqualTo(ConvertForCompare(actualDic[expectedKVP.Key])), $"expected {expectedKVP.Key}-{expectedKVP.Value}, actual {actualDic[expectedKVP.Key]}");
			}
		}

		public static object ConvertForCompare(object val)
		{
			if (val == null)
			{
				return null;
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
				case TypeCode.Single:
				case TypeCode.Double:
				case TypeCode.Decimal:
					return val.ToString();
			}

			return val;
		}

	}
}
