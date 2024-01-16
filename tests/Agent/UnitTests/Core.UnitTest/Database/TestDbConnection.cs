// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Data;

namespace NewRelic.Agent.Core.Database
{
    public class TestDbConnection : IDbConnection
    {
        public string ConnectionError { get; set; }
        public IDbTransaction BeginTransaction(IsolationLevel il)
        {
            throw new NotImplementedException();
        }

        public IDbTransaction BeginTransaction()
        {
            throw new NotImplementedException();
        }

        public void ChangeDatabase(string databaseName)
        {
            Database = databaseName;
        }

        public void Close()
        {
        }

        public string ConnectionString { get; set; }

        public int ConnectionTimeout { get; set; }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope")]
        public IDbCommand CreateCommand()
        {
            TestDatabaseCommand command = new TestDatabaseCommand("");
            command.Connection = this;
            return command;
        }

        public string Database { get; set; }

        public void Open()
        {
            if (ConnectionError != null)
            {
                throw new SimpleException(ConnectionError);
            }
        }

        public ConnectionState State { get; set; }

        public void Dispose()
        {
        }
    }
}
