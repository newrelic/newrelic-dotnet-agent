using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.Core.Configuration;
using NewRelic.Agent.Core.Database;
using NewRelic.Agent.Core.Metric;
using NewRelic.Agent.Core.Transactions;
using NewRelic.Agent.Core.Transactions.TransactionNames;
using NewRelic.Agent.Core.Transformers;
using NewRelic.Agent.Core.Transformers.TransactionTransformer;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Builders;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Data;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using Newtonsoft.Json;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.WireModels
{
    [TestFixture, Category("SqlTraces")]
    public class SqlTraceWireModelTests
    {
        private SqlTraceWireModel _sqlTraceWireModel;

        private const String TransactionName = "WebTransaction/ASP/post.aspx";
        private const String Uri = "http://localhost:8080/post.aspx";
        private const Int32 SqlId = 1530282818;
        private const String Sql = "Select * from meh";
        private const String DatabaseMetricName = "Database/be_datastoresettings/delete";
        private const UInt32 CallCount = 1;
        private static readonly TimeSpan TotalCallTime = TimeSpan.FromSeconds(1);
        private static readonly TimeSpan MinCallTime = TimeSpan.FromSeconds(1);
        private static readonly TimeSpan MaxCallTime = TimeSpan.FromSeconds(1);
        private readonly Dictionary<String, Object> _parameterData = new Dictionary<String, Object>();

        [SetUp]
        public void SetUp()
        {
            _sqlTraceWireModel = new SqlTraceWireModel(TransactionName, Uri, SqlId, Sql, DatabaseMetricName, CallCount, TotalCallTime, MinCallTime, MaxCallTime, _parameterData);
        }

        [Test]
        public void when_default_fixture_values_are_used_then_serializes_correctly()
        {
            const String expectedResult = "[\"WebTransaction/ASP/post.aspx\",\"http://localhost:8080/post.aspx\",1530282818,\"Select * from meh\",\"Database/be_datastoresettings/delete\",1,1000.0,1000.0,1000.0,{}]";

            var actualResult = JsonConvert.SerializeObject(_sqlTraceWireModel);
            Assert.AreEqual(expectedResult, actualResult);
        }

        [Test]
        public void multiple_sqlId_does_not_has_9_digits_number()
        {
            var transactionMetadata = new TransactionMetadata();
            var name = new WebTransactionName("foo", "bar");
            var metadata = transactionMetadata.ConvertToImmutableMetadata();
            var duration = TimeSpan.FromSeconds(1);
            var guid = Guid.NewGuid().ToString();
            var transactionMetricName = new TransactionMetricName("WebTransaction", "Name");
            var databaseService = new DatabaseService();
            var configurationService = Mock.Create<ConfigurationService>();
            String[] queries = {Sql, "Select * from someTable", "Insert x into anotherTable", "another random string",
                "1234567890!@#$%^&*()", "fjdksalfjdkla;fjdla;", "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
                "NNNNNNNNNNNUUUUUUUUUUUUUUUUTTTTTTTTTTTTTTTHHHHHHHHHHHHHIIIIIIIIIIIIIIIIIIIINNNNNNNNNNNNNNNNNNNN",
                Double.MaxValue.ToString()};
            var sqlTraceMaker = new SqlTraceMaker(configurationService);
            var traceDatas = new List<SqlTraceWireModel>();

            foreach (String query in queries)
            {
                var data = new DatastoreSegmentData()
                {
                    CommandText = query,
                    DatastoreVendorName = DatastoreVendor.MSSQL
                };
                var segments = new List<Segment>()
                    {
                        new TypedSegment<DatastoreSegmentData>(new TimeSpan(), TotalCallTime,
                            new TypedSegment<DatastoreSegmentData>(Mock.Create<ITransactionSegmentState>(),
                            new MethodCallData("typeName", "methodName", 1), data, false))
                    };
                var immutableTransaction = new ImmutableTransaction(name, segments, metadata, DateTime.Now, duration, guid, false, false, false, SqlObfuscator.GetObfuscatingSqlObfuscator());

                var sqlTraceData = sqlTraceMaker.TryGetSqlTrace(immutableTransaction, transactionMetricName, (TypedSegment<DatastoreSegmentData>)immutableTransaction.Segments.FirstOrDefault());
                traceDatas.Add(sqlTraceData);
            }

            foreach (SqlTraceWireModel traceData in traceDatas)
            {
                var numberOfDigits = Math.Floor(Math.Log10(traceData.SqlId) + 1);
                Assert.IsTrue(numberOfDigits != 9);
            }
        }

        [Test]
        public void when_construtor_used_TransactionName_property_is_set()
        {
            Assert.AreEqual(TransactionName, _sqlTraceWireModel.TransactionName);
        }

        [Test]
        public void when_construtor_used_uri_property_is_set()
        {
            Assert.AreEqual(Uri, _sqlTraceWireModel.Uri);
        }

        [Test]
        public void when_construtor_used_sqlId_property_is_set()
        {
            Assert.AreEqual(SqlId, _sqlTraceWireModel.SqlId);
        }

        [Test]
        public void when_construtor_used_sql_property_is_set()
        {
            Assert.AreEqual(Sql, _sqlTraceWireModel.Sql);
        }

        [Test]
        public void when_construtor_used_databaseMetricName_property_is_set()
        {
            Assert.AreEqual(DatabaseMetricName, _sqlTraceWireModel.DatastoreMetricName);
        }

        [Test]
        public void when_construtor_used_callcount_property_is_set()
        {
            Assert.AreEqual(CallCount, _sqlTraceWireModel.CallCount);
        }

        [Test]
        public void when_construtor_used_totalcalltime_property_is_set()
        {
            Assert.AreEqual(TotalCallTime, _sqlTraceWireModel.TotalCallTime);
        }

        [Test]
        public void when_construtor_used_mincalltime_property_is_set()
        {
            Assert.AreEqual(MinCallTime, _sqlTraceWireModel.MinCallTime);
        }

        [Test]
        public void when_construtor_used_maxcalltime_property_is_set()
        {
            Assert.AreEqual(MaxCallTime, _sqlTraceWireModel.MaxCallTime);
        }

        [Test]
        public void when_construtor_used_parameterdate_property_is_set()
        {
            Assert.AreEqual(_parameterData, _sqlTraceWireModel.ParameterData);
        }
    }
}
