using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NewRelic.Agent.Api;
using NewRelic.Agent.Core.Config;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Builders;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Testing.Assertions;
using NUnit.Framework;

namespace CompositeTests
{
	[TestFixture]
	public class DatastoreSegmentQueryParametersTests
	{
		private static CompositeTestAgent _compositeTestAgent;

		private IAgent _agent;

		[SetUp]
		public void SetUp()
		{
			_compositeTestAgent = new CompositeTestAgent();
			_agent = _compositeTestAgent.GetAgent();
		}

		[TearDown]
		public static void TearDown()
		{
			_compositeTestAgent.Dispose();
		}

		[Test]
		public void DatastoreSegment_HasQueryParameters()
		{
			SetupConfiguration(true);

			var queryParameters = new Dictionary<string, IConvertible>
			{
				{ "myKey1", "myValue1" }
			};

			var segment = CreateWebTransactionWithDatastoreSegment(queryParameters);

			var segmentData = (DatastoreSegmentData)segment.Data;

			CollectionAssert.AreEquivalent(segmentData.QueryParameters, new Dictionary<string, IConvertible>
			{
				{ "myKey1", "myValue1" }
			});
		}

		[Test]
		public void DatastoreSegment_HasNoQueryParameters()
		{
			SetupConfiguration(false);

			var queryParameters = new Dictionary<string, IConvertible>
			{
				{ "myKey1", "myValue1" }
			};

			var segment = CreateWebTransactionWithDatastoreSegment(queryParameters);

			var segmentData = (DatastoreSegmentData)segment.Data;

			Assert.AreEqual(segmentData.QueryParameters, null);
		}

		[Test]
		public void DatastoreSegment_ModifiedQueryParametersShouldNotBeAvailableInTheSegment()
		{
			SetupConfiguration(true);

			var queryParameters = new Dictionary<string, IConvertible>
			{
				{ "myKey1", "myValue1" }
			};

			var segment = CreateWebTransactionWithDatastoreSegment(queryParameters);

			//The modified query parameters should not affect the query parameters that were added to the segment
			queryParameters.Add("myKey2", "myValue2");
			queryParameters["myKey1"] = 1;

			var segmentData = (DatastoreSegmentData)segment.Data;

			CollectionAssert.AreEquivalent(segmentData.QueryParameters, new Dictionary<string, IConvertible>
			{
				{ "myKey1", "myValue1" }
			});
		}

		[Test]
		public void DatastoreSegment_ShouldHandleLongParameterNames()
		{
			var reallyLongString = new string('a', Agent.QueryParameterMaxStringLength + 1);

			SetupConfiguration(true);

			var queryParameters = new Dictionary<string, IConvertible>
			{
				{ reallyLongString, "myValue1" }
			};

			var segment = CreateWebTransactionWithDatastoreSegment(queryParameters);

			var segmentData = (DatastoreSegmentData)segment.Data;

			var truncatedName = new string('a', Agent.QueryParameterMaxStringLength);
			CollectionAssert.AreEquivalent(segmentData.QueryParameters, new Dictionary<string, IConvertible>
			{
				{ truncatedName, "myValue1" }
			});
		}

		[Test]
		public void DatastoreSegment_ShouldHandleBooleanParameters()
		{
			SetupConfiguration(true);

			var queryParameters = new Dictionary<string, IConvertible>
			{
				{ "myKey1", true }
			};

			var segment = CreateWebTransactionWithDatastoreSegment(queryParameters);

			var segmentData = (DatastoreSegmentData)segment.Data;

			CollectionAssert.AreEquivalent(segmentData.QueryParameters, new Dictionary<string, IConvertible>
			{
				{ "myKey1", true }
			});
		}

		[Test]
		public void DatastoreSegment_ShouldHandleNumericTypes()
		{
			SetupConfiguration(true);

			var queryParameters = new Dictionary<string, IConvertible>
			{
				{ "myint16", Int16.MaxValue },
				{ "myInt32", Int32.MaxValue },
				{ "myInt64", Int64.MaxValue },
				{ "myUInt16", UInt16.MaxValue },
				{ "myUInt32", UInt32.MaxValue },
				{ "myUInt64", UInt64.MaxValue },
				{ "mySByte", SByte.MaxValue },
				{ "myByte", Byte.MaxValue },
				{ "mySingle", Single.MaxValue },
				{ "myDouble", Double.MaxValue },
				{ "myDecimal", Decimal.MaxValue }
			};

			var segment = CreateWebTransactionWithDatastoreSegment(queryParameters);

			var segmentData = (DatastoreSegmentData)segment.Data;

			CollectionAssert.AreEquivalent(segmentData.QueryParameters, new Dictionary<string, IConvertible>
			{
				{ "myint16", Int16.MaxValue },
				{ "myInt32", Int32.MaxValue },
				{ "myInt64", Int64.MaxValue },
				{ "myUInt16", UInt16.MaxValue },
				{ "myUInt32", UInt32.MaxValue },
				{ "myUInt64", UInt64.MaxValue },
				{ "mySByte", SByte.MaxValue },
				{ "myByte", Byte.MaxValue },
				{ "mySingle", Single.MaxValue },
				{ "myDouble", Double.MaxValue },
				{ "myDecimal", Decimal.MaxValue }
			});
		}

		[Test]
		public void DatastoreSegment_ShouldHandleCharParameters()
		{
			SetupConfiguration(true);

			var queryParameters = new Dictionary<string, IConvertible>
			{
				{ "myChar1", 'c' }
			};

			var segment = CreateWebTransactionWithDatastoreSegment(queryParameters);

			var segmentData = (DatastoreSegmentData)segment.Data;

			CollectionAssert.AreEquivalent(segmentData.QueryParameters, new Dictionary<string, IConvertible>
			{
				{ "myChar1", 'c' }
			});
		}

		[Test]
		public void DatastoreSegment_ShouldHandleUnsupportedTypes()
		{
			SetupConfiguration(true);

			var now = DateTime.Now;
			var queryParameters = new Dictionary<string, IConvertible>
			{
				{ "myDateTime", now }
			};

			var segment = CreateWebTransactionWithDatastoreSegment(queryParameters);

			var segmentData = (DatastoreSegmentData)segment.Data;

			NrAssert.Multiple(
				() => Assert.AreEqual(1, segmentData.QueryParameters.Count),
				() => Assert.IsInstanceOf(typeof(string), segmentData.QueryParameters["myDateTime"]),
				() => Assert.AreEqual(now.ToString(CultureInfo.InvariantCulture), segmentData.QueryParameters["myDateTime"])
			);
		}

		[Test]
		public void DatastoreSegment_ShouldTruncateLongStringValues()
		{
			var reallyLongString = new string('a', Agent.QueryParameterMaxStringLength + 1);

			SetupConfiguration(true);

			var queryParameters = new Dictionary<string, IConvertible>
			{
				{ "myKey", reallyLongString }
			};

			var segment = CreateWebTransactionWithDatastoreSegment(queryParameters);

			var segmentData = (DatastoreSegmentData)segment.Data;

			var truncatedName = new string('a', Agent.QueryParameterMaxStringLength);
			CollectionAssert.AreEquivalent(segmentData.QueryParameters, new Dictionary<string, IConvertible>
			{
				{ "myKey", truncatedName }
			});
		}

		[Test]
		public void DatastoreSegment_ShouldHandleBadResultsFromObjectsConvertedToStrings()
		{
			SetupConfiguration(true);

			var queryParameters = new Dictionary<string, IConvertible>
			{
				{ "exceptionThrower", new ExceptionThrowingConvertible() },
				{ "longConvertible", new LongStringConvertible() },
				{ "NullConvertible", new NullStringConvertible() }
			};

			var segment = CreateWebTransactionWithDatastoreSegment(queryParameters);

			var segmentData = (DatastoreSegmentData)segment.Data;

			NrAssert.Multiple(
				() => Assert.AreEqual(1, segmentData.QueryParameters.Count),
				() => Assert.IsInstanceOf(typeof(string), segmentData.QueryParameters["longConvertible"]),
				() => Assert.AreEqual(new string('l', Agent.QueryParameterMaxStringLength), segmentData.QueryParameters["longConvertible"])
			);
		}

		[Test]
		public void DatastoreSegment_WithoutQueryParameters()
		{
			SetupConfiguration(true);

			var segment = CreateWebTransactionWithDatastoreSegment(null);

			var segmentData = (DatastoreSegmentData)segment.Data;

			Assert.IsNull(segmentData.QueryParameters);
		}

		private void SetupConfiguration(bool enableQueryParameters)
		{
			_compositeTestAgent.LocalConfiguration.datastoreTracer.queryParameters.enabled = enableQueryParameters;
			_compositeTestAgent.LocalConfiguration.transactionTracer.recordSql = configurationTransactionTracerRecordSql.raw;
			_compositeTestAgent.PushConfiguration();
		}

		private TypedSegment<DatastoreSegmentData> CreateWebTransactionWithDatastoreSegment(IDictionary<string, IConvertible> queryParameters)
		{
			_agent.CreateWebTransaction(WebTransactionType.Action, "name");
			return (TypedSegment<DatastoreSegmentData>)_agent.StartDatastoreRequestSegmentOrThrow("INSERT", DatastoreVendor.MSSQL, "MyAwesomeTable", null, null, "HostName", "1433", "MyDatabase", queryParameters);
		}

		private class ExceptionThrowingConvertible : IConvertible
		{
			public TypeCode GetTypeCode()
			{
				throw new NotImplementedException();
			}

			public bool ToBoolean(IFormatProvider provider)
			{
				throw new NotImplementedException();
			}

			public char ToChar(IFormatProvider provider)
			{
				throw new NotImplementedException();
			}

			public sbyte ToSByte(IFormatProvider provider)
			{
				throw new NotImplementedException();
			}

			public byte ToByte(IFormatProvider provider)
			{
				throw new NotImplementedException();
			}

			public short ToInt16(IFormatProvider provider)
			{
				throw new NotImplementedException();
			}

			public ushort ToUInt16(IFormatProvider provider)
			{
				throw new NotImplementedException();
			}

			public int ToInt32(IFormatProvider provider)
			{
				throw new NotImplementedException();
			}

			public uint ToUInt32(IFormatProvider provider)
			{
				throw new NotImplementedException();
			}

			public long ToInt64(IFormatProvider provider)
			{
				throw new NotImplementedException();
			}

			public ulong ToUInt64(IFormatProvider provider)
			{
				throw new NotImplementedException();
			}

			public float ToSingle(IFormatProvider provider)
			{
				throw new NotImplementedException();
			}

			public double ToDouble(IFormatProvider provider)
			{
				throw new NotImplementedException();
			}

			public decimal ToDecimal(IFormatProvider provider)
			{
				throw new NotImplementedException();
			}

			public DateTime ToDateTime(IFormatProvider provider)
			{
				throw new NotImplementedException();
			}

			public string ToString(IFormatProvider provider)
			{
				throw new NotImplementedException();
			}

			public object ToType(Type conversionType, IFormatProvider provider)
			{
				throw new NotImplementedException();
			}
		}

		private class LongStringConvertible : IConvertible
		{
			public TypeCode GetTypeCode()
			{
				throw new NotImplementedException();
			}

			public bool ToBoolean(IFormatProvider provider)
			{
				throw new NotImplementedException();
			}

			public char ToChar(IFormatProvider provider)
			{
				throw new NotImplementedException();
			}

			public sbyte ToSByte(IFormatProvider provider)
			{
				throw new NotImplementedException();
			}

			public byte ToByte(IFormatProvider provider)
			{
				throw new NotImplementedException();
			}

			public short ToInt16(IFormatProvider provider)
			{
				throw new NotImplementedException();
			}

			public ushort ToUInt16(IFormatProvider provider)
			{
				throw new NotImplementedException();
			}

			public int ToInt32(IFormatProvider provider)
			{
				throw new NotImplementedException();
			}

			public uint ToUInt32(IFormatProvider provider)
			{
				throw new NotImplementedException();
			}

			public long ToInt64(IFormatProvider provider)
			{
				throw new NotImplementedException();
			}

			public ulong ToUInt64(IFormatProvider provider)
			{
				throw new NotImplementedException();
			}

			public float ToSingle(IFormatProvider provider)
			{
				throw new NotImplementedException();
			}

			public double ToDouble(IFormatProvider provider)
			{
				throw new NotImplementedException();
			}

			public decimal ToDecimal(IFormatProvider provider)
			{
				throw new NotImplementedException();
			}

			public DateTime ToDateTime(IFormatProvider provider)
			{
				throw new NotImplementedException();
			}

			public string ToString(IFormatProvider provider)
			{
				return new string('l', Agent.QueryParameterMaxStringLength + 1);
			}

			public object ToType(Type conversionType, IFormatProvider provider)
			{
				throw new NotImplementedException();
			}
		}

		private class NullStringConvertible : IConvertible
		{
			public TypeCode GetTypeCode()
			{
				throw new NotImplementedException();
			}

			public bool ToBoolean(IFormatProvider provider)
			{
				throw new NotImplementedException();
			}

			public char ToChar(IFormatProvider provider)
			{
				throw new NotImplementedException();
			}

			public sbyte ToSByte(IFormatProvider provider)
			{
				throw new NotImplementedException();
			}

			public byte ToByte(IFormatProvider provider)
			{
				throw new NotImplementedException();
			}

			public short ToInt16(IFormatProvider provider)
			{
				throw new NotImplementedException();
			}

			public ushort ToUInt16(IFormatProvider provider)
			{
				throw new NotImplementedException();
			}

			public int ToInt32(IFormatProvider provider)
			{
				throw new NotImplementedException();
			}

			public uint ToUInt32(IFormatProvider provider)
			{
				throw new NotImplementedException();
			}

			public long ToInt64(IFormatProvider provider)
			{
				throw new NotImplementedException();
			}

			public ulong ToUInt64(IFormatProvider provider)
			{
				throw new NotImplementedException();
			}

			public float ToSingle(IFormatProvider provider)
			{
				throw new NotImplementedException();
			}

			public double ToDouble(IFormatProvider provider)
			{
				throw new NotImplementedException();
			}

			public decimal ToDecimal(IFormatProvider provider)
			{
				throw new NotImplementedException();
			}

			public DateTime ToDateTime(IFormatProvider provider)
			{
				throw new NotImplementedException();
			}

			public string ToString(IFormatProvider provider)
			{
				return null;
			}

			public object ToType(Type conversionType, IFormatProvider provider)
			{
				throw new NotImplementedException();
			}
		}

	}
}
