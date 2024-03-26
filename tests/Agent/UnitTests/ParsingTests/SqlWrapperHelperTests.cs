// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Data;
#if NETFRAMEWORK
using System.Data.OleDb;
using System.Data.OracleClient;
#endif
using System.Data.SqlClient;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Parsing;
using NUnit.Framework;

namespace SqlTests
{
    [TestFixture]
    public class SqlWrapperHelperTests
    {
        #region GetVendorName

#if NETFRAMEWORK
        [Test]
        [TestCase("SQL Server", ExpectedResult = DatastoreVendor.MSSQL)]
        [TestCase("MySql", ExpectedResult = DatastoreVendor.MySQL)]
        [TestCase("Oracle", ExpectedResult = DatastoreVendor.Oracle)]
        [TestCase("PostgreSQL", ExpectedResult = DatastoreVendor.Postgres)]
        [TestCase("IBMDB2", ExpectedResult = DatastoreVendor.IBMDB2)]
        public DatastoreVendor GetVendorName_ReturnsCorrectHost_IfOleDbConnectionProviderContainsKnownHost(
            string provider)
        {
            var command = new OleDbCommand
            {
                Connection = new OleDbConnection("Provider=" + provider)
            };

            return SqlWrapperHelper.GetVendorName(command);
        }
#endif
        [Test]
        [TestCase("SqlCommand", ExpectedResult = DatastoreVendor.MSSQL)]
        [TestCase("MySqlCommand", ExpectedResult = DatastoreVendor.MySQL)]
        [TestCase("OracleCommand", ExpectedResult = DatastoreVendor.Oracle)]
        [TestCase("OracleDatabase", ExpectedResult = DatastoreVendor.Oracle)]
        [TestCase("NpgsqlCommand", ExpectedResult = DatastoreVendor.Postgres)]
        [TestCase("DB2Command", ExpectedResult = DatastoreVendor.IBMDB2)]
        public DatastoreVendor
            GetVendorName_ReturnsCorrectHost(string typeName)
        {
            return SqlWrapperHelper.GetVendorName(typeName);
        }

        [Test]
        public void GetVendorName_ReturnsSqlServer_IfTypeNameIsNotProvidedAndCommandIsSqlCommand()
        {
            var command = new SqlCommand();

            var datastoreName = SqlWrapperHelper.GetVendorName(command);

            Assert.That(datastoreName, Is.EqualTo(DatastoreVendor.MSSQL));
        }
#if NETFRAMEWORK
        [Test]
        public void GetVendorName_ReturnsOracle_IfTypeNameIsNotProvidedAndCommandIsOracleCommand()
        {
#pragma warning disable 618    // Ignore deprecated warnings
            var command = new OracleCommand();
#pragma warning restore 618

            var datastoreName = SqlWrapperHelper.GetVendorName(command);

            Assert.That(datastoreName, Is.EqualTo(DatastoreVendor.Oracle));
        }
#endif
        [Test]
        public void GetVendorName_ReturnsUnknown_IfCommandIsOfUnknownType()
        {
            var command = new UnknownDbCommand();

            var datastoreName = SqlWrapperHelper.GetVendorName(command);

            Assert.That(datastoreName, Is.EqualTo(DatastoreVendor.Other));
        }

        public class UnknownDbCommand : IDbCommand
        {
            public void Dispose()
            {
                throw new NotImplementedException();
            }

            public void Prepare()
            {
                throw new NotImplementedException();
            }

            public void Cancel()
            {
                throw new NotImplementedException();
            }

            public IDbDataParameter CreateParameter()
            {
                throw new NotImplementedException();
            }

            public int ExecuteNonQuery()
            {
                throw new NotImplementedException();
            }

            public IDataReader ExecuteReader()
            {
                throw new NotImplementedException();
            }

            public IDataReader ExecuteReader(CommandBehavior behavior)
            {
                throw new NotImplementedException();
            }

            public object ExecuteScalar()
            {
                throw new NotImplementedException();
            }

            public IDbConnection Connection { get; set; }
            public IDbTransaction Transaction { get; set; }
            public string CommandText { get; set; }
            public int CommandTimeout { get; set; }
            public CommandType CommandType { get; set; }
            public IDataParameterCollection Parameters { get; set; }
            public UpdateRowSource UpdatedRowSource { get; set; }
        }

        #endregion GetVendorName
    }
}
