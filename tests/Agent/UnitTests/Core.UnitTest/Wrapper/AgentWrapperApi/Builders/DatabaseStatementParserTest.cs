// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Core.Config;
using NewRelic.Agent.Core.Configuration;
using NewRelic.Agent.Core.Events;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.SystemInterfaces;
using NewRelic.SystemInterfaces.Web;
using System.Data;
using System.Threading;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Builders
{

    [TestFixture]
    public class DatabaseStatementParserTest
    {
        private DatabaseStatementParser _databaseStatementParser;

        [SetUp]
        public void SetUp()
        {
            _databaseStatementParser = new DatabaseStatementParser();
        }

        [TearDown]
        public void TearDown()
        {
            _databaseStatementParser.Dispose();
        }

        [Test]
        public void ParseDatabaseStatement_SameStatementSameVendor_Matched()
        {
            var statement = _databaseStatementParser.ParseDatabaseStatement(DatastoreVendor.MSSQL, CommandType.Text, "select * from users");
            var statement2 = _databaseStatementParser.ParseDatabaseStatement(DatastoreVendor.MSSQL, CommandType.Text, "select * from users");

            //Use AreSame to ensure that we are getting a reference match.
            ClassicAssert.AreSame(statement, statement2);
        }

        [Test]
        public void ParseDatabaseStatement_DifferentStatementsSameVendor_NotMatched()
        {
            var statement = _databaseStatementParser.ParseDatabaseStatement(DatastoreVendor.MSSQL, CommandType.Text, "select * from users");
            var statement2 = _databaseStatementParser.ParseDatabaseStatement(DatastoreVendor.MSSQL, CommandType.Text, "select * from people");

            ClassicAssert.AreNotSame(statement, statement2);
        }

        [Test]
        public void ParseDatabaseStatement_SameStatementDifferentVendor_NotMatched()
        {
            var statement = _databaseStatementParser.ParseDatabaseStatement(DatastoreVendor.MSSQL, CommandType.Text, "select * from users");
            var statement2 = _databaseStatementParser.ParseDatabaseStatement(DatastoreVendor.Oracle, CommandType.Text, "select * from users");

            ClassicAssert.AreNotSame(statement, statement2);
        }


        [Test]
        public void ParseDatabaseStatement_CommandTypeNotText_IsNotCached()
        {
            var statement1 = _databaseStatementParser.ParseDatabaseStatement(DatastoreVendor.MSSQL, CommandType.StoredProcedure, "pHelloWorld");
            var statement2 = _databaseStatementParser.ParseDatabaseStatement(DatastoreVendor.MSSQL, CommandType.StoredProcedure, "pHelloWorld");

            var statement3 = _databaseStatementParser.ParseDatabaseStatement(DatastoreVendor.MSSQL, CommandType.TableDirect, "users");
            var statement4 = _databaseStatementParser.ParseDatabaseStatement(DatastoreVendor.MSSQL, CommandType.TableDirect, "users");

            ClassicAssert.AreNotSame(statement1, statement2);
            ClassicAssert.AreNotSame(statement3, statement4);
        }

        [Test]
        public void CacheCapacity_ChangesApplied()
        {
            // A configuration service is instantiated here because it will
            // subscribe to the ConfigurationDeserializedEvent fired in SetCacheCapcity.
            var configurationService = new ConfigurationService(
                Mock.Create<IEnvironment>(),
                Mock.Create<IProcessStatic>(),
                Mock.Create<IHttpRuntimeStatic>(),
                Mock.Create<IConfigurationManagerStatic>(),
                Mock.Create<IDnsStatic>());

            const string sql1 = "select * from table1";
            const string sql2 = "select * from table2";
            const string sql3 = "select * from table3";

            //Set initial capacity of cache to 2
            SetCacheCapacity(2);

            _databaseStatementParser.ParseDatabaseStatement(DatastoreVendor.MSSQL, CommandType.Text, sql1);
            _databaseStatementParser.ParseDatabaseStatement(DatastoreVendor.MSSQL, CommandType.Text, sql2);
            var stmtA = _databaseStatementParser.ParseDatabaseStatement(DatastoreVendor.MSSQL, CommandType.Text, sql3);

            Thread.Sleep(1000);//Allow cache to periodically clean

            var stmtB = _databaseStatementParser.ParseDatabaseStatement(DatastoreVendor.MSSQL, CommandType.Text, sql3);

            //stmtA and stmtB are the same SQL, but stmtA was ejected from the cache because of the cache periodically cleanup, so they cannot be the same object reference
            //This tests our original capacity is being honored.
            ClassicAssert.AreNotSame(stmtA, stmtB);

            //Resize the cache
            SetCacheCapacity(3);

            _databaseStatementParser.ParseDatabaseStatement(DatastoreVendor.MSSQL, CommandType.Text, sql1);
            _databaseStatementParser.ParseDatabaseStatement(DatastoreVendor.MSSQL, CommandType.Text, sql2);

            Thread.Sleep(1000);//Allow cache to periodically clean

            //stmtB and stmtC are the same SQL, but this time nothing was ejected because of the cache size is withing its capacity, so they are the same object reference
            var stmtC = _databaseStatementParser.ParseDatabaseStatement(DatastoreVendor.MSSQL, CommandType.Text, sql3);
            ClassicAssert.AreSame(stmtB, stmtC);

            configurationService.Dispose();
        }

        private void SetCacheCapacity(int capacity)
        {
            var newConfiguration = new configuration()
            {
                appSettings = new System.Collections.Generic.List<configurationAdd>()
                {
                    new configurationAdd() {key = "SqlStatementCacheCapacity", value = capacity.ToString()}
                }
            };
            EventBus<ConfigurationDeserializedEvent>.Publish(new ConfigurationDeserializedEvent(newConfiguration));
        }
    }
}
