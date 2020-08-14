// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.Core.Configuration;
using NewRelic.Agent.Core.Database;
using NewRelic.Agent.Core.Transactions;
using NewRelic.Agent.Core.Transformers.TransactionTransformer;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Data;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using Newtonsoft.Json;
using NUnit.Framework;
using Telerik.JustMock;
using NewRelic.Agent.Extensions.Parsing;
using NewRelic.Agent.Core.Attributes;
using NewRelic.Agent.Core.Segments;
using NewRelic.Agent.Core.AgentHealth;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Segments.Tests;
using NewRelic.Agent.Core.Spans;

namespace NewRelic.Agent.Core.WireModels
{
    [TestFixture, Category("SqlTraces")]
    public class SqlTraceWireModelTests
    {
        private SqlTraceWireModel _sqlTraceWireModel;

        private const string TrxDisplayName = "WebTransaction/ASP/post.aspx";
        private const string Uri = "http://localhost:8080/post.aspx";
        private const int SqlId = 1530282818;
        private const string Sql = "Select * from meh";
        private const string DatabaseMetricName = "Database/be_datastoresettings/delete";
        private const uint CallCount = 1;
        private static readonly TimeSpan TotalCallTime = TimeSpan.FromSeconds(1);
        private static readonly TimeSpan MinCallTime = TimeSpan.FromSeconds(1);
        private static readonly TimeSpan MaxCallTime = TimeSpan.FromSeconds(1);
        private IAttributeDefinitionService _attribDefSvc;
        private IAttributeDefinitions _attribDefs => _attribDefSvc.AttributeDefs;

        private readonly Dictionary<string, object> _parameterData = new Dictionary<string, object>();

        [SetUp]
        public void SetUp()
        {
            _sqlTraceWireModel = new SqlTraceWireModel(TrxDisplayName, Uri, SqlId, Sql, DatabaseMetricName, CallCount, TotalCallTime, MinCallTime, MaxCallTime, _parameterData);
            _attribDefSvc = new AttributeDefinitionService((f) => new AttributeDefinitions(f));
        }

        [Test]
        public void when_default_fixture_values_are_used_then_serializes_correctly()
        {
            const string expectedResult = "[\"WebTransaction/ASP/post.aspx\",\"http://localhost:8080/post.aspx\",1530282818,\"Select * from meh\",\"Database/be_datastoresettings/delete\",1,1000.0,1000.0,1000.0,{}]";

            var actualResult = JsonConvert.SerializeObject(_sqlTraceWireModel);
            Assert.AreEqual(expectedResult, actualResult);
        }

        [Test]
        public void multiple_sqlId_does_not_has_9_digits_number()
        {
            var transactionMetadata = new TransactionMetadata();
            var name = TransactionName.ForWebTransaction("foo", "bar");
            var metadata = transactionMetadata.ConvertToImmutableMetadata();
            var duration = TimeSpan.FromSeconds(1);
            var guid = Guid.NewGuid().ToString();
            var transactionMetricName = new TransactionMetricName("WebTransaction", "Name");
            var databaseService = new DatabaseService(Mock.Create<ICacheStatsReporter>());
            var configurationService = Mock.Create<IConfigurationService>();
            var attribDefSvc = new AttributeDefinitionService((f) => new AttributeDefinitions(f));

            string[] queries = {Sql, "Select * from someTable", "Insert x into anotherTable", "another random string",
                "1234567890!@#$%^&*()", "fjdksalfjdkla;fjdla;", "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
                "NNNNNNNNNNNUUUUUUUUUUUUUUUUTTTTTTTTTTTTTTTHHHHHHHHHHHHHIIIIIIIIIIIIIIIIIIIINNNNNNNNNNNNNNNNNNNN",
                double.MaxValue.ToString()};
            var sqlTraceMaker = new SqlTraceMaker(configurationService, attribDefSvc, databaseService);
            var traceDatas = new List<SqlTraceWireModel>();

            foreach (string query in queries)
            {
                var data = new DatastoreSegmentData(databaseService, new ParsedSqlStatement(DatastoreVendor.MSSQL, null, null), query);
                var segment = new Segment(TransactionSegmentStateHelpers.GetItransactionSegmentState(), new MethodCallData("typeName", "methodName", 1));
                segment.SetSegmentData(data);

                var segments = new List<Segment>()
                    {
                        new Segment(new TimeSpan(), TotalCallTime, segment, null)
                    };
                var immutableTransaction = new ImmutableTransaction(name, segments, metadata, DateTime.Now, duration, duration, guid, false, false, false, 1.2f, false, string.Empty, null, _attribDefs);

                var sqlTraceData = sqlTraceMaker.TryGetSqlTrace(immutableTransaction, transactionMetricName, immutableTransaction.Segments.FirstOrDefault());
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
            Assert.AreEqual(TrxDisplayName, _sqlTraceWireModel.TransactionName);
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
