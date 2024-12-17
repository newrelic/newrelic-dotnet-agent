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
using NewRelic.Agent.Extensions.Parsing;
using NUnit.Framework;

namespace ParsingTests
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
        public DatastoreVendor GetVendorName_ReturnsCorrectVendor_IfOleDbConnectionProviderContainsKnownProvider(
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
            GetVendorName_ReturnsCorrectVendor(string typeName)
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

        [Test]
        [TestCase("DRIVER={SQL Server Native Client 11.0};Server=127.0.0.1;Database=NewRelic;Trusted_Connection=no;UID=sa;PWD=password;Encrypt=no;", ExpectedResult = DatastoreVendor.MSSQL)]
        [TestCase("Driver={MySQL ODBC 5.2 UNICODE Driver};Server=localhost;Database=myDataBase;User=myUsername;Password=myPassword;Option=3;", ExpectedResult = DatastoreVendor.MySQL)]
        [TestCase("Driver={Microsoft ODBC for Oracle};Server=myServerAddress;Uid=myUsername;Pwd=myPassword;", ExpectedResult = DatastoreVendor.Oracle)]
        [TestCase("Driver={Oracle in OraClient11g_home1};Dbq=myTNSServiceName;Uid=myUsername;Pwd=myPassword;", ExpectedResult = DatastoreVendor.Oracle)]
        [TestCase("Driver={PostgreSQL UNICODE};Server=IP address;Port=5432;Database=myDataBase;Uid=myUsername;Pwd=myPassword;", ExpectedResult = DatastoreVendor.Postgres)]
        [TestCase("Driver={IBM DB2 ODBC DRIVER};Database=myDataBase;Hostname=myServerAddress;Port=1234;Protocol=TCPIP;Uid=myUsername;Pwd=myPassword;", ExpectedResult = DatastoreVendor.IBMDB2)]
        [TestCase("Driver={Amazon Redshift ODBC Driver (x64)};Database=myDataBase;Hostname=myServerAddress;Port=1234;Protocol=TCPIP;Uid=myUsername;Pwd=myPassword;", ExpectedResult = DatastoreVendor.ODBC)]
        [TestCase("Driver={MyCoolDb ODBC DRIVER};Database=myDataBase;Hostname=myServerAddress;Port=1234;Protocol=TCPIP;Uid=myUsername;Pwd=myPassword;", ExpectedResult = DatastoreVendor.ODBC)]
        public DatastoreVendor GetVendorNameFromOdbcConnectionString_ReturnsExpectedVendor(string connectionString)
        {
            return SqlWrapperHelper.GetVendorNameFromOdbcConnectionString(connectionString);
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
