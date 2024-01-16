// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using System.Data;

namespace ParsingTests
{
    // Using a concrete mock so that we can have a mock object that implements 2 unrelated but expected interfaces
    internal class MockDbConnection : IDbConnection, ICloneable
    {
        public string ConnectionString { get; set; }

        public int ConnectionTimeout => 0;

        public string Database => null;

        public ConnectionState State => ConnectionState.Closed;

        public List<IDbCommand> CreatedMockCommands { get; private set; } = new List<IDbCommand>();

        public IDbTransaction BeginTransaction()
        {
            return null;
        }

        public IDbTransaction BeginTransaction(IsolationLevel il)
        {
            return null;
        }

        public void ChangeDatabase(string databaseName)
        {
        }

        public object Clone()
        {
            return new MockDbConnection();
        }

        public void Close()
        {
        }

        public IDbCommand CreateCommand()
        {
            var command = new MockDbCommand();
            CreatedMockCommands.Add(command);

            return command;
        }

        public void Dispose()
        {
        }

        public void Open()
        {
        }
    }
}
