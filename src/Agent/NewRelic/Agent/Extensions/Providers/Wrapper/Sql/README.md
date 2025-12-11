# New Relic .NET Agent Sql Instrumentation

## Overview

The Sql instrumentation wrapper provides automatic monitoring for SQL database operations across multiple database providers and drivers within existing transactions. It creates datastore segments for command execution, tracks data reader operations (optional), and monitors connection open operations. The wrapper supports SQL Server, Oracle, MySQL, PostgreSQL, DB2, ODBC, and OLE DB providers.

## Instrumented Methods

### OdbcCommandTracer (System.Data)
- **Wrapper**: [OdbcCommandWrapper.cs](OdbcCommandWrapper.cs)
- **Assembly**: `System.Data`
- **Type**: `System.Data.Odbc.OdbcCommand`

| Method | Creates Transaction | Requires Existing Transaction |
|--------|-------------------|------------------------------|
| ExecuteNonQuery | No | Yes |
| ExecuteReader | No | Yes |
| ExecuteScalar | No | Yes |

### OdbcCommandTracer (System.Data.Odbc)
- **Wrapper**: [OdbcCommandWrapper.cs](OdbcCommandWrapper.cs)
- **Assembly**: `System.Data.Odbc`
- **Type**: `System.Data.Odbc.OdbcCommand`

| Method | Creates Transaction | Requires Existing Transaction |
|--------|-------------------|------------------------------|
| ExecuteNonQuery | No | Yes |
| ExecuteReader | No | Yes |
| ExecuteScalar | No | Yes |

### OleDbCommandTracer
- **Wrapper**: [OleDbCommandWrapper.cs](OleDbCommandWrapper.cs)
- **Assembly**: `System.Data`
- **Type**: `System.Data.OleDb.OleDbCommand`

| Method | Creates Transaction | Requires Existing Transaction |
|--------|-------------------|------------------------------|
| ExecuteNonQuery | No | Yes |
| ExecuteReader | No | Yes |
| ExecuteScalar | No | Yes |

### SqlCommandTracer (SQL Server - System.Data)
- **Wrapper**: [SqlCommandWrapper.cs](SqlCommandWrapper.cs)
- **Assembly**: `System.Data`
- **Type**: `System.Data.SqlClient.SqlCommand`

| Method | Creates Transaction | Requires Existing Transaction |
|--------|-------------------|------------------------------|
| ExecuteNonQuery | No | Yes |
| ExecuteReader | No | Yes |
| ExecuteScalar | No | Yes |
| ExecuteXmlReader | No | Yes |

### SqlCommandTracer (SQL Server - System.Data.SqlClient)
- **Wrapper**: [SqlCommandWrapper.cs](SqlCommandWrapper.cs)
- **Assembly**: `System.Data.SqlClient`
- **Type**: `System.Data.SqlClient.SqlCommand`

| Method | Creates Transaction | Requires Existing Transaction |
|--------|-------------------|------------------------------|
| ExecuteNonQuery | No | Yes |
| ExecuteReader | No | Yes |
| ExecuteScalar | No | Yes |
| ExecuteXmlReader | No | Yes |

### SqlCommandTracer (SQL Server - Microsoft.Data.SqlClient)
- **Wrapper**: [SqlCommandWrapper.cs](SqlCommandWrapper.cs)
- **Assembly**: `Microsoft.Data.SqlClient`
- **Type**: `Microsoft.Data.SqlClient.SqlCommand`

| Method | Creates Transaction | Requires Existing Transaction |
|--------|-------------------|------------------------------|
| ExecuteNonQuery | No | Yes |
| ExecuteReader | No | Yes |
| ExecuteScalar | No | Yes |
| ExecuteXmlReader | No | Yes |

### SqlCommandTracer (Oracle - Multiple Providers)
Supports Oracle.DataAccess, Oracle.ManagedDataAccess, Devart.Data.Oracle, and System.Data.OracleClient

| Method | Creates Transaction | Requires Existing Transaction |
|--------|-------------------|------------------------------|
| ExecuteNonQuery | No | Yes |
| ExecuteReader / ExecuteReaderInternal | No | Yes |
| ExecuteScalar | No | Yes |
| ExecuteXmlReader | No | Yes (where supported) |

### SqlCommandTracer (MySQL - Multiple Providers)
Supports MySql.Data, MySqlConnector, and Devart.Data.MySql

| Method | Creates Transaction | Requires Existing Transaction |
|--------|-------------------|------------------------------|
| ExecuteDbDataReader | No | Yes |
| ExecuteNonQuery | No | Yes |
| ExecuteReader | No | Yes |
| ExecuteScalar | No | Yes |

### SqlCommandTracer (PostgreSQL - Npgsql)
- **Assembly**: `Npgsql`
- **Type**: `Npgsql.NpgsqlCommand`

| Method | Creates Transaction | Requires Existing Transaction |
|--------|-------------------|------------------------------|
| ExecuteDbDataReader | No | Yes |
| ExecuteNonQuery | No | Yes |
| ExecuteReader | No | Yes |
| ExecuteScalar | No | Yes |

### SqlCommandTracer (IBM DB2)
- **Assembly**: `IBM.Data.DB2`
- **Type**: `IBM.Data.DB2.DB2Command`

| Method | Creates Transaction | Requires Existing Transaction |
|--------|-------------------|------------------------------|
| ExecuteNonQuery | No | Yes |
| ExecuteReader | No | Yes |
| ExecuteScalar | No | Yes |
| ExecuteXmlReader | No | Yes |

### SqlCommandTracerAsync
Async variants of all the above commands across all supported providers.

### DataReaderTracer (DISABLED by default)
Instruments `Read`, `NextResult`, and `TryFastRead` methods for various DataReader implementations.

**Note**: DataReader instrumentation is disabled by default due to potential performance impact. Set `enabled="true"` in instrumentation.xml to enable.

### OpenConnectionTracer
Instruments `Open` and `OpenAsync` methods for database connections across all supported providers.

[Instrumentation.xml](https://github.com/newrelic/newrelic-dotnet-agent/blob/main/src/Agent/NewRelic/Agent/Extensions/Providers/Wrapper/Sql/Instrumentation.xml)

## Supported Database Providers

### SQL Server
- **System.Data.SqlClient** (Framework and .NET Core/NuGet)
- **Microsoft.Data.SqlClient** (Flagship driver for .NET Core and Framework)

### Oracle
- **System.Data.OracleClient** (Deprecated)
- **Oracle.DataAccess** (Vendor driver)
- **Oracle.ManagedDataAccess** (Managed vendor driver, multiple versions)
- **Devart.Data.Oracle** (Third-party driver)

### MySQL
- **MySql.Data** (Official driver)
- **MySqlConnector** (Community driver, versions 0.x and 1.x)
- **Devart.Data.MySql** (Third-party driver)

### PostgreSQL
- **Npgsql** (Official driver, versions 4.0+)

### IBM DB2
- **IBM.Data.DB2** (IBM driver)

### ODBC & OLE DB
- **System.Data.Odbc** (Framework and .NET Core)
- **System.Data.OleDb** (Framework)

## Attributes Added

The wrapper creates datastore segments with the following attributes:

- **Datastore vendor**: Database type (e.g., "MSSQL", "Oracle", "MySQL", "Postgres", "DB2", "ODBC", "OLEDB")
- **Database name**: Retrieved from connection string
- **Server host and port**: Retrieved from connection string
- **SQL statement**: SQL command text (subject to obfuscation based on agent configuration)
- **Operation**: SQL operation type parsed from command text (SELECT, INSERT, UPDATE, DELETE, etc.)

## Version Considerations

### Oracle.ManagedDataAccess
- **Versions < 4.122.23**: Instruments `ExecuteReader` methods
- **Versions >= 4.122.23 (.NET Framework)**: Instruments `ExecuteReaderInternal` method
- **Versions >= 23.0.0 (.NET 6+)**: Instruments `ExecuteReaderInternal` method

### MySql.Data
- **Versions < 8.0.33**: Instruments synchronous methods only
- **Versions >= 8.0.33**: Instruments async methods (sync methods became passthroughs to async)

### MySqlConnector
- **Version 0.x**: Uses `MySql.Data.MySqlClient` namespace
- **Version 1.x**: Uses `MySqlConnector` namespace

### Npgsql
- **Versions 4.0 - 4.1**: Instruments specific method signatures
- **Versions 4.1 - 5.0**: Adds `TryFastRead` instrumentation
- **Versions 5.0+**: Enhanced instrumentation coverage

## DataReader Instrumentation

DataReader instrumentation is **disabled by default** due to potential performance overhead. To enable:

1. Set `enabled="true"` on the `DataReaderTracer` and `DataReaderTracerAsync` tracerFactory elements
2. Restart the application

When enabled, the agent creates segments for:
- **Read**: Individual row reads
- **NextResult**: Moving to next result set
- **TryFastRead**: Optimized read operations (Npgsql only)

## Connection Instrumentation

The wrapper monitors connection open operations to track:
- Connection establishment time
- Connection pool metrics
- Database server connectivity

Both synchronous (`Open`) and asynchronous (`OpenAsync`) connection methods are instrumented across all supported providers.

## License
Copyright 2020 New Relic, Inc. All rights reserved.
SPDX-License-Identifier: Apache-2.0
