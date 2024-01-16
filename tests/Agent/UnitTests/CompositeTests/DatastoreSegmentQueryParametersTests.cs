// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Api;
using NewRelic.Agent.Core;
using NewRelic.Agent.Core.Config;
using NewRelic.Agent.Core.Segments;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Testing.Assertions;
using System.Globalization;

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

            ClassicAssert.AreEqual(segmentData.QueryParameters, null);
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
                { "myint16", short.MaxValue },
                { "myint", int.MaxValue },
                { "myInt64", long.MaxValue },
                { "myUInt16", ushort.MaxValue },
                { "myUint", uint.MaxValue },
                { "myUInt64", ulong.MaxValue },
                { "mySByte", sbyte.MaxValue },
                { "myByte", byte.MaxValue },
                { "mySingle", float.MaxValue },
                { "myDouble", double.MaxValue },
                { "myDecimal", decimal.MaxValue }
            };

            var segment = CreateWebTransactionWithDatastoreSegment(queryParameters);

            var segmentData = (DatastoreSegmentData)segment.Data;

            CollectionAssert.AreEquivalent(segmentData.QueryParameters, new Dictionary<string, IConvertible>
            {
                { "myint16", short.MaxValue },
                { "myint", int.MaxValue },
                { "myInt64", long.MaxValue },
                { "myUInt16", ushort.MaxValue },
                { "myUint", uint.MaxValue },
                { "myUInt64", ulong.MaxValue },
                { "mySByte", sbyte.MaxValue },
                { "myByte", byte.MaxValue },
                { "mySingle", float.MaxValue },
                { "myDouble", double.MaxValue },
                { "myDecimal", decimal.MaxValue }
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
                () => ClassicAssert.AreEqual(1, segmentData.QueryParameters.Count),
                () => ClassicAssert.IsInstanceOf(typeof(string), segmentData.QueryParameters["myDateTime"]),
                () => ClassicAssert.AreEqual(now.ToString(CultureInfo.InvariantCulture), segmentData.QueryParameters["myDateTime"])
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
                () => ClassicAssert.AreEqual(1, segmentData.QueryParameters.Count),
                () => ClassicAssert.IsInstanceOf(typeof(string), segmentData.QueryParameters["longConvertible"]),
                () => ClassicAssert.AreEqual(new string('l', Agent.QueryParameterMaxStringLength), segmentData.QueryParameters["longConvertible"])
            );
        }

        [Test]
        public void DatastoreSegment_WithoutQueryParameters()
        {
            SetupConfiguration(true);

            var segment = CreateWebTransactionWithDatastoreSegment(null);

            var segmentData = (DatastoreSegmentData)segment.Data;

            ClassicAssert.IsNull(segmentData.QueryParameters);
        }

        private void SetupConfiguration(bool enableQueryParameters)
        {
            _compositeTestAgent.LocalConfiguration.datastoreTracer.queryParameters.enabled = enableQueryParameters;
            _compositeTestAgent.LocalConfiguration.transactionTracer.recordSql = configurationTransactionTracerRecordSql.raw;
            _compositeTestAgent.PushConfiguration();
        }

        private Segment CreateWebTransactionWithDatastoreSegment(IDictionary<string, IConvertible> queryParameters)
        {
            _agent.CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.Action),
                transactionDisplayName: "name",
                doNotTrackAsUnitOfWork: true);

            return (Segment)_agent.StartDatastoreRequestSegmentOrThrow("INSERT", DatastoreVendor.MSSQL, "MyAwesomeTable", null, null, "HostName", "1433", "MyDatabase", queryParameters);
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
