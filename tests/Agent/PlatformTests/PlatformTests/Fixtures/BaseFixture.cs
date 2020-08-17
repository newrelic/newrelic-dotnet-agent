// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using PlatformTests.Applications;
using Xunit.Abstractions;

namespace PlatformTests.Fixtures
{
    public class BaseFixture
    {
        public BaseApplication Application { get; }
        public ITestOutputHelper TestLogger { get; set; }

        public BaseFixture(BaseApplication application)
        {
            Application = application;
        }

        public Action Exercise { get; set; }

        public void Initialize()
        {
            Application.TestLogger = TestLogger;

            Application.InstallAgent();

            Application.BuildAndDeploy();

            TestLogger?.WriteLine($@"[{DateTime.Now}] ... Testing");

            Exercise.Invoke();

            TestLogger?.WriteLine($@"[{DateTime.Now}] ... Tesing done");

            Application.StopTestApplicationService();
        }
    }
}
