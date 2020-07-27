using System;
using System.Net;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Parsing.ConnectionString;
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

        [TestCase(DatastoreVendor.Oracle, "win-database.pdx.vm.datanerd.us", "1234", null, null, @"SERVER=(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST=win-database.pdx.vm.datanerd.us)(PORT=1234))(CONNECT_DATA=(SERVICE_NAME=MyOracleSID)));uid=myUsername;pwd=myPassword;")]
        [TestCase(DatastoreVendor.Oracle, "111.21.31.99", "1234", null, null, @"Driver=(Oracle in XEClient);dbq=111.21.31.99:1234/XE;Uid=myUsername;Pwd=myPassword;")]
        [TestCase(DatastoreVendor.Oracle, "111.21.31.99", "1234", null, null, @"Driver=(Oracle in XEClient);dbq=111.21.31.99:1234;Uid=myUsername;Pwd=myPassword;")]
        [TestCase(DatastoreVendor.Oracle, "myserver", "1234", null, null, @"Data Source=username/password@//myserver:1234/my.service.com;")]
        [TestCase(DatastoreVendor.Oracle, "myserver", "default", null, null, @"Data Source=username/password@//myserver/my.service.com;")]
        [TestCase(DatastoreVendor.Oracle, "myserver", "1234", null, null, @"Data Source=username/password@myserver:1234/myservice:dedicated/instancename;")]
        [TestCase(DatastoreVendor.Oracle, "myserver", "default", null, null, @"Data Source=username/password@myserver/myservice:dedicated/instancename;")]
        [TestCase(DatastoreVendor.Oracle, "myserver", "default", null, null, @"Data Source=username/password@myserver//instancename;")]

        [TestCase(DatastoreVendor.MySQL, "myServerAddress", "default", "myDataBase", null, @"Server=myServerAddress;Database=myDataBase;")]
        [TestCase(DatastoreVendor.MySQL, "myServerAddress", "1234", "myDataBase", null, @"Data Source=myServerAddress;Port=1234;Database=myDataBase;")]
        [TestCase(DatastoreVendor.MySQL, "myServerAddress", "1234", "myDataBase", null, @"Network Address=myServerAddress;Port=1234;Database=myDataBase;")]
        [TestCase(DatastoreVendor.MySQL, "myServerAddress", "1234", "myDataBase", null, @"Server=myServerAddress;Port=1234;Database=myDataBase;")]
        [TestCase(DatastoreVendor.MySQL, null, null, "myDataBase", null, @"Server=serverAddress1, serverAddress2, serverAddress3;Database=myDataBase;")]

        [TestCase(DatastoreVendor.Postgres, "myServerAddress", "5432", "myDataBase", null, @"Server=myServerAddress;Port=5432;Database=myDataBase;")]
        [TestCase(DatastoreVendor.Postgres, "myServerAddress", "1234", "myDataBase", null, @"Host=myServerAddress;Port=1234;Database=myDataBase;")]
        [TestCase(DatastoreVendor.Postgres, "myServerAddress", "1234", "myDataBase", null, @"Data Source=myServerAddress;Port=1234;Location=myDataBase;")]

        [TestCase(DatastoreVendor.IBMDB2, "myServerAddress", "1234", "myDataBase", null, @"Server=myServerAddress:1234;Database=myDataBase;")]
        [TestCase(DatastoreVendor.IBMDB2, "myServerAddress", "1234", "myDataBase", null, @"Network Address=myServerAddress:1234;Database=myDataBase;")]
        [TestCase(DatastoreVendor.IBMDB2, "myServerAddress", "1234", "myDataBase", null, @"Hostname=myServerAddress:1234;Database=myDataBase;")]

        [TestCase(DatastoreVendor.Redis, "123.123.123.123", "1234", null, null, "123.123.123.123:1234")]
        [TestCase(DatastoreVendor.Redis, "win-database.pdx.vm.datanerd.us", "234", null, null, "win-database.pdx.vm.datanerd.us:234,password=NOPERS")]
        [TestCase(DatastoreVendor.Redis, "win-database.pdx.vm.datanerd.us", null, null, null, "win-database.pdx.vm.datanerd.us,password=NOPERS")]
        [TestCase(DatastoreVendor.Redis, "hostname_of_localhost", null, null, null, "localhost,password=NOPERS")]
        [TestCase(DatastoreVendor.Redis, "hostname_of_localhost", null, null, null, "127.0.0.1,password=NOPERS")]

        public void TestConnectionStringParsing(DatastoreVendor vendor, String expectedHost, String expectedPathPortOrId, String expectedDatabaseName, String expectedInstanceName, String connectionString)
        {
            if (expectedHost == "hostname_of_localhost")
            {
                expectedHost = Dns.GetHostName();
            }

            var connectionInfo = ConnectionInfo.FromConnectionString(vendor, connectionString);
            Assert.True(connectionInfo.Host == expectedHost);
            Assert.True(connectionInfo.PortPathOrId == expectedPathPortOrId);
            Assert.True(connectionInfo.DatabaseName == expectedDatabaseName);
            Assert.True(connectionInfo.InstanceName == expectedInstanceName);
        }
    }
}
