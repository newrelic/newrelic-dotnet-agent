// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MultiFunctionApplicationHelpers
{
    public class ConsoleDynamicMethodFixtureFWLatest : ConsoleDynamicMethodFixtureFW48
    {
        public ConsoleDynamicMethodFixtureFWLatest()
        {
        }
    }


    /// <summary>
    /// Use this fixture for High Security Mode tests
    /// </summary>
    public class ConsoleDynamicMethodFixtureFWLatestHSM : ConsoleDynamicMethodFixtureFW48
    {
        public override string TestSettingCategory { get { return "HSM"; } }
        public ConsoleDynamicMethodFixtureFWLatestHSM()
        {
        }
    }

    /// <summary>
    /// Use this fixture for Configurable Security Policy tests
    /// </summary>
    public class ConsoleDynamicMethodFixtureFWLatestCSP : ConsoleDynamicMethodFixtureFW48
    {
        public override string TestSettingCategory { get { return "CSP"; } }
        public ConsoleDynamicMethodFixtureFWLatestCSP()
        {
        }
    }

    public class ConsoleDynamicMethodFixtureFW48 : ConsoleDynamicMethodFixtureFWSpecificVersion
    {
        public ConsoleDynamicMethodFixtureFW48() : base("net48")
        {
        }
    }

    public class ConsoleDynamicMethodFixtureFW471 : ConsoleDynamicMethodFixtureFWSpecificVersion
    {
        public ConsoleDynamicMethodFixtureFW471() : base("net471")
        {
        }
    }

    public class ConsoleDynamicMethodFixtureFW462 : ConsoleDynamicMethodFixtureFWSpecificVersion
    {
        public ConsoleDynamicMethodFixtureFW462() : base("net462")
        {
        }
    }

    public abstract class ConsoleDynamicMethodFixtureFWSpecificVersion : ConsoleDynamicMethodFixture
    {
        private static readonly string ApplicationDirectoryName = @"ConsoleMultiFunctionApplicationFW";
        private static readonly string ExecutableName = $"{ApplicationDirectoryName}.exe";

        /// <summary>
        /// Use this .ctor to specify a specific .NET framework version to target.
        /// </summary>
        /// <param name="targetFramework">The framework target to use when running the application. This parameter must match one of the targetFramework values defined in ConsoleMultiFunctionApplicationFW.csproj </param>
        public ConsoleDynamicMethodFixtureFWSpecificVersion(string targetFramework) : base(ApplicationDirectoryName, ExecutableName, targetFramework, false, DefaultTimeout)
        {
        }
    }


    public class ConsoleDynamicMethodFixtureCore21 : ConsoleDynamicMethodFixtureCoreSpecificVersion
    {
        public ConsoleDynamicMethodFixtureCore21() : base("netcoreapp2.1")
        {
        }
    }

    public class ConsoleDynamicMethodFixtureCore22 : ConsoleDynamicMethodFixtureCoreSpecificVersion
    {
        public ConsoleDynamicMethodFixtureCore22() : base("netcoreapp2.2")
        {
        }
    }

    public class ConsoleDynamicMethodFixtureCore31 : ConsoleDynamicMethodFixtureCoreSpecificVersion
    {
        public ConsoleDynamicMethodFixtureCore31() : base("netcoreapp3.1")
        {
        }
    }

    public class ConsoleDynamicMethodFixtureCore50 : ConsoleDynamicMethodFixtureCoreSpecificVersion
    {
        public ConsoleDynamicMethodFixtureCore50() : base("net5.0")
        {
        }
    }

    public class ConsoleDynamicMethodFixtureCore60 : ConsoleDynamicMethodFixtureCoreSpecificVersion
    {
        public ConsoleDynamicMethodFixtureCore60() : base("net6.0")
        {
        }
    }

    /// <summary>
    /// Use this fixture if you don't care about which .net core version the test application should use.
    /// If you need to test against a feature that belongs to a specific .net core version, then consider
    /// using one of the existing specific version fixtures, or create a new specific version.
    /// When testing newer .net core preview releases, this targetFramework version should be updated.
    /// </summary>
    public class ConsoleDynamicMethodFixtureCoreLatest : ConsoleDynamicMethodFixtureCore60
    {
        public ConsoleDynamicMethodFixtureCoreLatest()
        {
        }
    }

    /// <summary>
    /// Use this fixture for High Security Mode tests
    /// </summary>
    public class ConsoleDynamicMethodFixtureCoreLatestHSM : ConsoleDynamicMethodFixtureCore60
    {
        public override string TestSettingCategory { get { return "HSM"; } }
        public ConsoleDynamicMethodFixtureCoreLatestHSM()
        {
        }

    }

    /// <summary>
    /// Use this fixture for Configurable Security Policy tests
    /// </summary>
    public class ConsoleDynamicMethodFixtureCoreLatestCSP : ConsoleDynamicMethodFixtureCore60
    {
        public override string TestSettingCategory { get { return "CSP"; } }
        public ConsoleDynamicMethodFixtureCoreLatestCSP()
        {
        }

    }

    public abstract class ConsoleDynamicMethodFixtureCoreSpecificVersion : ConsoleDynamicMethodFixture
    {
        private static readonly string ApplicationDirectoryName = @"ConsoleMultiFunctionApplicationCore";
        private static readonly string ExecutableName = $"{ApplicationDirectoryName}.exe";

        /// <summary>
        /// Use this .ctor to specify a specific .net core version to target.
        /// </summary>
        /// <param name="targetFramework">The netcoreapp target use when publishing and running the application. This parameter must match one of the targetFramework values defined in ConsoleMultiFunctionApplicationCore.csproj </param>
        public ConsoleDynamicMethodFixtureCoreSpecificVersion(string targetFramework) : base(ApplicationDirectoryName, ExecutableName, targetFramework, true, DefaultTimeout)
        {
        }
    }

    public abstract class ConsoleDynamicMethodFixture : RemoteApplicationFixture
    {
        protected static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

        protected override int MaxTries => 1;       //No need to retry this;

        private List<string> _commands = new List<string>();

        public new RemoteConsoleApplication RemoteApplication => base.RemoteApplication as RemoteConsoleApplication;

        public string IntegrationTestAppPath => RemoteApplication.SourceApplicationsDirectoryPath;

        public ConsoleDynamicMethodFixture SetTimeout(TimeSpan span)
        {
            RemoteApplication.SetTimeout(span);
            return this;
        }

        public ConsoleDynamicMethodFixture AddCommand(params string[] commands)
        {
            _commands.AddRange(commands.Where(x => !string.IsNullOrWhiteSpace(x)));
            return this;
        }

        public ConsoleDynamicMethodFixture(string applicationDirectoryName, string executableName, string targetFramework, bool isCoreApp, TimeSpan timeout)
            : base(new RemoteConsoleApplication(applicationDirectoryName, executableName, targetFramework, ApplicationType.Shared, isCoreApp, isCoreApp)
                  .SetTimeout(timeout)
                  .ExposeStandardInput(true))
        {
            Actions(exerciseApplication: () =>
            {
                foreach (var cmd in _commands)
                {
                    if (!RemoteApplication.IsRunning)
                    {
                        throw new Exception($"Remote Process has exited, Cannot execute command: {cmd}");
                    }

                    RemoteApplication.WriteToStandardInput(cmd);
                }
                RemoteApplication.WriteToStandardInput("exit");
            });
        }
    }
}
