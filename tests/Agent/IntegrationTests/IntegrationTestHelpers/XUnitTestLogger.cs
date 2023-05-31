// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Diagnostics;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTestHelpers
{
    public class XUnitTestLogger : ITestLogger
    {
        private readonly ITestOutputHelper _xunitOutput;
        private readonly string _child;
        public int ChildPid;

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
            _xunitOutput?.WriteLine($"{DateTime.Now} [Test Runner ({Process.GetCurrentProcess().Id})] {message}");
        }

        public void WriteLine(string format, params object[] args)
        {
            _xunitOutput?.WriteLine($"{DateTime.Now} [Test Runner ({Process.GetCurrentProcess().Id})] {string.Format(format, args)}");
        }
    }
}
