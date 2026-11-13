// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Data;

namespace ParsingTests;

// Using a concrete mock so that we can have a mock object that implements 2 unrelated but expected interfaces
internal class MockDbConnection : IDbConnection, ICloneable
{
    public string ConnectionString { get; set; }

    public int ConnectionTimeout => 0;

    public string Database => null;

    public ConnectionState State => ConnectionState.Closed;

    public List<IDbCommand> CreatedMockCommands { get; private set; } = new List<IDbCommand>();

    // SetShowPlan always creates exactly two commands per GenerateExplainPlan call, ON then OFF, so "the second
    // one" reliably means the OFF command -- letting a test simulate it also failing (on top of some other
    // failure) without needing to match on its CommandText.
    public Exception ThrowOnSecondCreatedCommandExecute { get; set; }

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
        if (CreatedMockCommands.Count == 1 && ThrowOnSecondCreatedCommandExecute != null)
        {
            command.ExecuteNonQueryException = ThrowOnSecondCreatedCommandExecute;
        }

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