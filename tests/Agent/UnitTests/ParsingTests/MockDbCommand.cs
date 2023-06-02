// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Data;
using System.Data.SqlClient;
using Telerik.JustMock;

namespace ParsingTests
{
    // Using a concrete mock so that we can have a mock object that implements 2 unrelated but expected interfaces
    internal class MockDbCommand : IDbCommand, ICloneable
    {
        public IDbConnection Connection { get; set; }
        public IDbTransaction Transaction { get; set; }
        public string CommandText { get; set; }
        public int CommandTimeout { get; set; }
        public CommandType CommandType { get; set; }

        public IDataParameterCollection Parameters { get; private set; }

        public UpdateRowSource UpdatedRowSource { get; set; }

        public IDataReader MockDataReader { get; private set; } = Mock.Create<IDataReader>();

        public MockDbCommand()
        {
            // Using a SqlCommand to create a SqlParameters instance that we can use to simplify creating test parameters.
            // It is easier to take this approach than using a synthetic or concrete mock.
            var emptyConnection = new SqlConnection("Server=falsehost;Database=fakedb;User Id=afakeuser;Password=notarealpasword;"); // not used for anything
            var sqlCommand = new SqlCommand(string.Empty, emptyConnection);

            Parameters = sqlCommand.Parameters;
        }

        public void Cancel()
        {
        }

        public object Clone()
        {
            return new MockDbCommand();
        }

        public IDbDataParameter CreateParameter()
        {
            return null;
        }

        public void Dispose()
        {
        }

        public int ExecuteNonQuery()
        {
            return 0;
        }

        public IDataReader ExecuteReader()
        {
            return MockDataReader;
        }

        public IDataReader ExecuteReader(CommandBehavior behavior)
        {
            return MockDataReader;
        }

        public object ExecuteScalar()
        {
            return null;
        }

        public void Prepare()
        {
        }
    }
}
