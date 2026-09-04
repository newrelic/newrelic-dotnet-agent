// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using Xunit;

namespace NewRelic.Agent.IntegrationTestHelpers;

public class XUnitTestLogger : ITestLogger
{
    // xunit holds ITestOutputHelper writes until the test ends, so a test that
    // hangs reports nothing at all. When this is set, progress lines also go to
    // the console, which reaches the CI log immediately. The bulk dumps from
    // WriteFormattedOutput stay out of it.
    private static readonly bool LiveLog = Environment.GetEnvironmentVariable("NR_DOTNET_TEST_LIVE_LOG") == "1";

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
        WriteSafe(Stamp(message), live: true);
    }

    public void WriteLine(string format, params object[] args)
    {
        WriteSafe(string.Format(Stamp(format), args), live: true);
    }

    public void WriteFormattedOutput(string formattedOutput)
    {
        WriteSafe(formattedOutput, live: false);
    }

    private static string Stamp(string message)
    {
        return $"[{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ss.fffZ}] {message}";
    }

    // xunit v3 throws once the test has ended, so fixture Dispose hits it.
    private void WriteSafe(string message, bool live)
    {
        var wroteToConsole = false;
        if (live && LiveLog)
        {
            Console.WriteLine(message);
            wroteToConsole = true;
        }

        try
        {
            _xunitOutput?.WriteLine(message);
        }
        catch (InvalidOperationException)
        {
            if (!wroteToConsole)
            {
                Console.WriteLine(message);
            }
        }
    }
}