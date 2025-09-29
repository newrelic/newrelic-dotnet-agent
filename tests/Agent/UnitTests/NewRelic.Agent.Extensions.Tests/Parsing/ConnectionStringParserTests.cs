// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Extensions.Parsing;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Agent.Extensions.Parsing.ConnectionString;
using NUnit.Framework;

namespace ParsingTests
{
    [TestFixture]
    public class ConnectionStringParserTests
    {
        [Test]
        [TestCase(DatastoreVendor.MSSQL, "win-database.pdx.vm.datanerd.us", "default", "NewRelic", "SQLEXPRESS", @"Server=win-database.pdx.vm.datanerd.us\SQLEXPRESS;Database=NewRelic;")]
        [TestCase(DatastoreVendor.MSSQL, "win-database.pdx.vm.datanerd.us", "1433", "NewRelic", "SQLEXPRESS", @"Server=win-database.pdx.vm.datanerd.us,1433\SQLEXPRESS;Database=NewRelic;")]
        [TestCase(DatastoreVendor.MSSQL, "win-database.pdx.vm.datanerd.us", "1433", "NewRelic", null, @"Server=win-database.pdx.vm.datanerd.us,1433;Database=NewRelic;")]
        [TestCase(DatastoreVendor.MSSQL, "win-database.pdx.vm.datanerd.us", "default", "NewRelic", null, @"Server=win-database.pdx.vm.datanerd.us;Database=NewRelic;")]
        [TestCase(DatastoreVendor.MSSQL, "hostname_of_localhost", "1433", "NewRelic", null, @"Data Source=localhost,1433;Initial Catalog=NewRelic;")]
        [TestCase(DatastoreVendor.MSSQL, "hostname_of_localhost", "1433", "NewRelic", null, @"Server=localhost,1433;Database=NewRelic;")]
        [TestCase(DatastoreVendor.MSSQL, "hostname_of_localhost", "1433", "NewRelic", null, @"Server=127.0.0.1,1433;Database=NewRelic;")]
        [TestCase(DatastoreVendor.MSSQL, "hostname_of_localhost", "1433", "NewRelic", null, @"Server=0:0:0:0:0:0:0:1,1433;Database=NewRelic;")]
        [TestCase(DatastoreVendor.MSSQL, "hostname_of_localhost", "1433", "NewRelic", null, @"Server=::1,1433;Database=NewRelic;")]
        // Added malformed MSSQL cases to confirm graceful degradation (should not throw)
        [TestCase(DatastoreVendor.MSSQL, "myHost", "unknown", "MyDb", null, @"Server=myHost,;Database=MyDb;")]
        [TestCase(DatastoreVendor.MSSQL, "myHost", "unknown", "MyDb", "myInstance", @"Server=myHost,\myInstance;Database=MyDb;")]
        [TestCase(DatastoreVendor.MSSQL, "myHost", "unknown", "MyDb", "myInstance", @"Server=myHost\myInstance,;Database=MyDb;")]            // host\instance, (trailing comma -> unknown port)
        [TestCase(DatastoreVendor.MSSQL, "myHost", "unknown", "MyDb", "myInstance", @"Server=myHost,\myInstance;Database=MyDb;")]            // host,\instance (empty port segment)
        [TestCase(DatastoreVendor.MSSQL, "myHost", "unknown", "MyDb", null, @"Server=myHost,;Database=MyDb;")]                               // host, (trailing comma, no instance)
        [TestCase(DatastoreVendor.MSSQL, "myHost", "1433", "MyDb", "myInstance", @"Server=myHost,1433\myInstance;Database=MyDb;")]           // host,port\instance
        [TestCase(DatastoreVendor.MSSQL, "hostname_of_localhost", "1433", "NewRelic", "SQLEXPRESS", @"Server=127.0.0.1\SQLEXPRESS,1433;Database=NewRelic;")] // loopback IPv4 host\instance,port
        [TestCase(DatastoreVendor.MSSQL, "hostname_of_localhost", "1433", "NewRelic", "SQLEXPRESS", @"Server=::1\SQLEXPRESS,1433;Database=NewRelic;")]       // loopback IPv6 host\instance,port

        [TestCase(DatastoreVendor.Oracle, "win-database.pdx.vm.datanerd.us", "1234", "unknown", null, @"SERVER=(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST=win-database.pdx.vm.datanerd.us)(PORT=1234))(CONNECT_DATA=(SERVICE_NAME=MyOracleSID)));uid=myUsername;pwd=myPassword;")]
        [TestCase(DatastoreVendor.Oracle, "111.21.31.99", "1234", "unknown", null, @"Driver=(Oracle in XEClient);dbq=111.21.31.99:1234/XE;Uid=myUsername;Pwd=myPassword;")]
        [TestCase(DatastoreVendor.Oracle, "111.21.31.99", "1234", "unknown", null, @"Driver=(Oracle in XEClient);dbq=111.21.31.99:1234;Uid=myUsername;Pwd=myPassword;")]
        [TestCase(DatastoreVendor.Oracle, "myserver", "1234", "unknown", null, @"Data Source=username/password@//myserver:1234/my.service.com;")]
        [TestCase(DatastoreVendor.Oracle, "myserver", "default", "unknown", null, @"Data Source=username/password@//myserver/my.service.com;")]
        [TestCase(DatastoreVendor.Oracle, "myserver", "1234", "unknown", null, @"Data Source=username/password@myserver:1234/myservice:dedicated/instancename;")]
        [TestCase(DatastoreVendor.Oracle, "myserver", "default", "unknown", null, @"Data Source=username/password@myserver/myservice:dedicated/instancename;")]
        [TestCase(DatastoreVendor.Oracle, "myserver", "default", "unknown", null, @"Data Source=username/password@myserver//instancename;")]

        [TestCase(DatastoreVendor.MySQL, "myServerAddress", "default", "myDataBase", null, @"Server=myServerAddress;Database=myDataBase;")]
        [TestCase(DatastoreVendor.MySQL, "myServerAddress", "1234", "myDataBase", null, @"Data Source=myServerAddress;Port=1234;Database=myDataBase;")]
        [TestCase(DatastoreVendor.MySQL, "myServerAddress", "1234", "myDataBase", null, @"Network Address=myServerAddress;Port=1234;Database=myDataBase;")]
        [TestCase(DatastoreVendor.MySQL, "myServerAddress", "1234", "myDataBase", null, @"Server=myServerAddress;Port=1234;Database=myDataBase;")]
        [TestCase(DatastoreVendor.MySQL, "unknown", "unknown", "myDataBase", null, @"Server=serverAddress1, serverAddress2, serverAddress3;Database=myDataBase;")]

        [TestCase(DatastoreVendor.Postgres, "myServerAddress", "5432", "myDataBase", null, @"Server=myServerAddress;Port=5432;Database=myDataBase;")]
        [TestCase(DatastoreVendor.Postgres, "myServerAddress", "1234", "myDataBase", null, @"Host=myServerAddress;Port=1234;Database=myDataBase;")]
        [TestCase(DatastoreVendor.Postgres, "myServerAddress", "1234", "myDataBase", null, @"Data Source=myServerAddress;Port=1234;Location=myDataBase;")]

        [TestCase(DatastoreVendor.IBMDB2, "myServerAddress", "1234", "myDataBase", null, @"Server=myServerAddress:1234;Database=myDataBase;")]
        [TestCase(DatastoreVendor.IBMDB2, "myServerAddress", "1234", "myDataBase", null, @"Network Address=myServerAddress:1234;Database=myDataBase;")]
        [TestCase(DatastoreVendor.IBMDB2, "myServerAddress", "1234", "myDataBase", null, @"Hostname=myServerAddress:1234;Database=myDataBase;")]

        [TestCase(DatastoreVendor.Redis, "123.123.123.123", "1234", "unknown", null, "123.123.123.123:1234")]
        [TestCase(DatastoreVendor.Redis, "win-database.pdx.vm.datanerd.us", "234", "unknown", null, "win-database.pdx.vm.datanerd.us:234,password=NOPERS")]
        [TestCase(DatastoreVendor.Redis, "win-database.pdx.vm.datanerd.us", "unknown", "unknown", null, "win-database.pdx.vm.datanerd.us,password=NOPERS")]
        [TestCase(DatastoreVendor.Redis, "hostname_of_localhost", "unknown", "unknown", null, "localhost,password=NOPERS")]
        [TestCase(DatastoreVendor.Redis, "hostname_of_localhost", "unknown", "unknown", null, "127.0.0.1,password=NOPERS")]

        // A lot of the following connection strings are contrived examples for code coverage purposes
        [TestCase(DatastoreVendor.ODBC, "examplecluster.abc123xyz789.us-west-2.redshift.amazonaws.com", "5439", "dev", null, "Driver={Amazon Redshift (x64)};Server=examplecluster.abc123xyz789.us-west-2.redshift.amazonaws.com;Database=dev;UID=adminuser;PWD=secret;Port=5439")]
        [TestCase(DatastoreVendor.ODBC, "hostname_of_localhost", "5439", "dev", null, "Driver={Amazon Redshift (x64)};Hostname=localhost;Database=dev;UID=adminuser;PWD=secret;Port=5439")]
        [TestCase(DatastoreVendor.ODBC, "myServerAddress", "1234", "myDataBase", null, "Driver={Amazon Redshift ODBC Driver (x64)};Database=myDataBase;Data source=myServerAddress;Port=1234;Protocol=TCPIP;Uid=myUsername;Pwd=myPassword;")]
        [TestCase(DatastoreVendor.ODBC, "mySequelServerHost", "1234", "myDataBase", "someInstance", "Driver={ODBC Driver for Sequel Server};Server=mySequelServerHost,1234\\someInstance;Database=myDataBase")]
        [TestCase(DatastoreVendor.ODBC, "mySequelServerHost", "1234", "myDataBase", "someInstance", "Driver={ODBC Driver for Sequel Server};Server=mySequelServerHost\\someInstance,1234;Database=myDataBase")]
        [TestCase(DatastoreVendor.ODBC, "mySequelServerHost", "unknown", "myDataBase", "someInstance", "Driver={ODBC Driver for Sequel Server};Server=mySequelServerHost,\\someInstance;Database=myDataBase")]
        [TestCase(DatastoreVendor.ODBC, "myReddishServerHost", "234", "unknown", null, "Driver={ODBC Driver for Reddish Server};Server=myReddishServerHost:234")]
        [TestCase(DatastoreVendor.ODBC, "hostname_of_localhost", "unknown", "unknown", null, "localhost,password=NOPERS")]
        [TestCase(DatastoreVendor.ODBC, "hostname_of_localhost", "unknown", "unknown", null, "127.0.0.1,password=NOPERS")]

        public void TestConnectionStringParsing(DatastoreVendor vendor, string expectedHost, string expectedPathPortOrId, string expectedDatabaseName, string expectedInstanceName, string connectionString)
        {
            ConnectionInfo connectionInfo = null;
            Assert.DoesNotThrow(() =>
                {
                    connectionInfo = ConnectionInfoParser.FromConnectionString(vendor, connectionString, "hostname_of_localhost");
                });

            Assert.Multiple(() =>
            {
                Assert.That(connectionInfo.Host, Is.EqualTo(expectedHost));
                Assert.That(connectionInfo.PortPathOrId, Is.EqualTo(expectedPathPortOrId));
                Assert.That(connectionInfo.DatabaseName, Is.EqualTo(expectedDatabaseName));
                Assert.That(connectionInfo.InstanceName, Is.EqualTo(expectedInstanceName));
            });
        }

        [Test]
        public void ConnectionStringParser_SameConnectionStrings_Matched()
        {
            var connectionString1 = @"Server=win-database.pdx.vm.datanerd.us\SQLEXPRESS;Database=NewRelic;";
            var connectionString2 = @"Server=win-database.pdx.vm.datanerd.us\SQLEXPRESS;Database=NewRelic;";

            var connectionInfo1 = ConnectionInfoParser.FromConnectionString(DatastoreVendor.MSSQL, connectionString1, "localhost");
            var connectionInfo2 = ConnectionInfoParser.FromConnectionString(DatastoreVendor.MSSQL, connectionString2, "localhost");

            Assert.That(connectionInfo2, Is.SameAs(connectionInfo1));
        }

        [Test]
        public void ConnectionStringParser_DifferentConnectionStrings_NotMatched()
        {
            var connectionString1 = @"Server=win-database.pdx.vm.datanerd.us\SQLEXPRESS;Database=NewRelic;";
            var connectionString2 = @"Server=win-database.pdx.vm.datanerd.us,1433\SQLEXPRESS;Database=NewRelic;";

            var connectionInfo1 = ConnectionInfoParser.FromConnectionString(DatastoreVendor.MSSQL, connectionString1, "localhost");
            var connectionInfo2 = ConnectionInfoParser.FromConnectionString(DatastoreVendor.MSSQL, connectionString2, "localhost");

            Assert.That(connectionInfo2, Is.Not.SameAs(connectionInfo1));
        }

        [Test]
        public void ConnectionStringParser_UnknownVendor_EmptyResult()
        {
            var connectionString = @"Driver={Microsoft Text Driver (*.txt; *.csv)};Dbq=c:\txtFilesFolder\;Extensions=asc,csv,tab,txt;";
            ConnectionInfo connectionInfo = null;
            Assert.DoesNotThrow(() =>
                {
                    connectionInfo = ConnectionInfoParser.FromConnectionString(DatastoreVendor.Other, connectionString, "localhost");
                });

            Assert.That(connectionInfo.Host, Is.EqualTo("unknown"));
            Assert.That(connectionInfo.PortPathOrId, Is.EqualTo("unknown"));
            Assert.That(connectionInfo.DatabaseName, Is.EqualTo("unknown"));
            Assert.That(connectionInfo.InstanceName, Is.Null);
        }

        [Test]
        public void MsSql_MalformedConnectionStrings_DoNotThrow()
        {
            var utilizationHostName = "hostname_of_localhost";

            // Each tuple: connectionString, expectedPortPathOrId, expectedInstanceName
            var cases = new (string cs, string expectedPort, string expectedInstance)[]
            {
                // Original negative-length risk patterns (backslash before comma)
                (@"Server=myHost\myInstance,1433;Database=Db;", "1433", "myInstance"),   // host\instance,port
                (@"Server=127.0.0.1\SQLEXPRESS,1433;Database=Db;", "1433", "SQLEXPRESS"),
                (@"Server=::1\SQLEXPRESS,1433;Database=Db;", "1433", "SQLEXPRESS"),

                // Port after instance but missing (trailing comma)
                (@"Server=myHost\myInstance,;Database=Db;", "unknown", "myInstance"),

                // Empty port segment before instance
                (@"Server=myHost,\myInstance;Database=Db;", "unknown", "myInstance"),

                // Port before instance (already covered in main parameterized tests but kept for explicit exception handling verification)
                (@"Server=myHost,1433\myInstance;Database=Db;", "1433", "myInstance"),

                // Instance only, no port
                (@"Server=myHost\myInstance;Database=Db;", "default", "myInstance"),

                // Trailing comma with no instance
                (@"Server=myHost,;Database=Db;", "unknown", null),

                // Multiple commas
                (@"Server=myHost,,1433;Database=Db;", "1433", null),

                // Leading comma then port
                (@"Server=,1433;Database=Db;", "1433", null),

                // Leading comma then instance and port
                (@"Server=,\myInstance,1433;Database=Db;", "1433", "myInstance"),

                // Just delimiters
                (@"Server=,;Database=Db;", "unknown", null),
            };

            foreach (var (cs, expectedPort, expectedInstance) in cases)
            {
                ConnectionInfo info = null;
                Assert.DoesNotThrow(() =>
                {
                    info = ConnectionInfoParser.FromConnectionString(DatastoreVendor.MSSQL, cs, utilizationHostName);
                }, $"Parser threw for MSSQL connection string: {cs}");

                Assert.Multiple(() =>
                {
                    Assert.That(info, Is.Not.Null, cs);
                    // Host may be empty -> becomes "unknown"
                    Assert.That(info.Host, Is.Not.Null.And.Not.Empty, cs);
                    Assert.That(info.PortPathOrId, Is.EqualTo(expectedPort), $"Port mismatch for: {cs}");
                    Assert.That(info.InstanceName, Is.EqualTo(expectedInstance), $"Instance mismatch for: {cs}");
                });
            }
        }

        [Test]
        public void Odbc_MalformedConnectionStrings_DoNotThrow()
        {
            var utilizationHostName = "hostname_of_localhost";

            // Each tuple: connectionString, expectedPortPathOrId, expectedInstanceName
            var cases = new (string cs, string expectedPort, string expectedInstance)[]
            {
                // host\instance,port
                (@"Driver={ODBC Driver for Sequel Server};Server=myHost\myInstance,1234;Database=Db", "1234", "myInstance"),
                // host\instance, (missing port)
                (@"Driver={ODBC Driver for Sequel Server};Server=myHost\myInstance,;Database=Db", "unknown", "myInstance"),
                // host,\instance (empty port before instance)
                (@"Driver={ODBC Driver for Sequel Server};Server=myHost,\myInstance;Database=Db", "unknown", "myInstance"),
                // host,port\instance
                (@"Driver={ODBC Driver for Sequel Server};Server=myHost,1234\myInstance;Database=Db", "1234", "myInstance"),
                // host, (trailing comma)
                (@"Driver={ODBC Driver for Sequel Server};Server=myHost,;Database=Db", "unknown", null),
                // multiple commas
                (@"Driver={ODBC Driver for Sequel Server};Server=myHost,,1234;Database=Db", "1234", null),
            };

            foreach (var (cs, expectedPort, expectedInstance) in cases)
            {
                ConnectionInfo info = null;
                Assert.DoesNotThrow(() =>
                {
                    info = ConnectionInfoParser.FromConnectionString(DatastoreVendor.ODBC, cs, utilizationHostName);
                }, $"Parser threw for ODBC connection string: {cs}");

                Assert.Multiple(() =>
                {
                    Assert.That(info, Is.Not.Null, cs);
                    Assert.That(info.Host, Is.Not.Null.And.Not.Empty, cs);
                    Assert.That(info.PortPathOrId, Is.EqualTo(expectedPort), $"Port mismatch for: {cs}");
                    Assert.That(info.InstanceName, Is.EqualTo(expectedInstance), $"Instance mismatch for: {cs}");
                });
            }
        }
    }
}
