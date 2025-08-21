// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using Xunit;

namespace NewRelic.Agent.IntegrationTestHelpers
{
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
            var line = $"[{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ss.fffZ}] {message}";
            if (_xunitOutput == null)
            {
                Console.WriteLine(line);
                return;
            }
            try
            {
                _xunitOutput.WriteLine(line);
            }
            catch (InvalidOperationException)
            {
                // Happens if xUnit test context already finished (e.g., fixture Dispose after test completion)
                // Fall back to console so cleanup diagnostics are not lost.
                Console.WriteLine(line);
            }
        }

        public void WriteLine(string format, params object[] args)
        {
            var timestampedFormat = $"[{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ss.fffZ}] {format}";
            if (_xunitOutput == null)
            {
                try
                {
                    Console.WriteLine(string.Format(timestampedFormat, args));
                }
                catch
                {
                    Console.WriteLine(timestampedFormat);
                }
                return;
            }
            try
            {
                _xunitOutput.WriteLine(timestampedFormat, args);
            }
            catch (InvalidOperationException)
            {
                // See comment above – write to console if test already finished.
                try
                {
                    Console.WriteLine(string.Format(timestampedFormat, args));
                }
                catch
                {
                    Console.WriteLine(timestampedFormat);
                }
            }
        }

        public void WriteFormattedOutput(string formattedOutput)
        {
            if (_xunitOutput == null)
            {
                Console.WriteLine(formattedOutput);
                return;
            }
            try
            {
                _xunitOutput.WriteLine(formattedOutput);
            }
            catch (InvalidOperationException)
            {
                Console.WriteLine(formattedOutput);
            }
        }
    }
}
