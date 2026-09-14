// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using Xunit;

namespace NewRelic.Agent.IntegrationTestHelpers;

public class XUnitTestLogger : ITestLogger
{
    private readonly ITestOutputHelper _xunitOutput;

    public XUnitTestLogger(ITestOutputHelper xunitOutput)
    {
        _xunitOutput = xunitOutput;

        if (_xunitOutput == null)
        {
            Console.WriteLine("XUnitTestLogger: xunitOutput was null. no data will be logged.");
        }
    }

    public void WriteLine(string message)
    {
        WriteSafe(Stamp(message));
    }

    public void WriteLine(string format, params object[] args)
    {
        WriteSafe(string.Format(Stamp(format), args));
    }

    public void WriteFormattedOutput(string formattedOutput)
    {
        WriteSafe(formattedOutput);
    }

    private static string Stamp(string message)
    {
        return $"[{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ss.fffZ}] {message}";
    }

    // xunit v3 throws once the test has ended, so fixture Dispose hits it.
    private void WriteSafe(string message)
    {
        try
        {
            _xunitOutput?.WriteLine(message);
        }
        catch (InvalidOperationException)
        {
            Console.WriteLine(message);
        }
    }
}