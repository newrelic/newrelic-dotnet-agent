// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures
{
    public class ConsoleDynamicMethodFixtureFWLatest : ConsoleDynamicMethodFixtureFW481
    {
        public ConsoleDynamicMethodFixtureFWLatest()
        {
        }
    }


    /// <summary>
    /// Use this fixture for High Security Mode tests
    /// </summary>
    public class ConsoleDynamicMethodFixtureFWLatestHSM : ConsoleDynamicMethodFixtureFW481
    {
        public override string TestSettingCategory { get { return "HSM"; } }
        public ConsoleDynamicMethodFixtureFWLatestHSM()
        {
        }
    }

    /// <summary>
    /// Use this fixture for Configurable Security Policy tests
    /// </summary>
    public class ConsoleDynamicMethodFixtureFWLatestCSP : ConsoleDynamicMethodFixtureFW481
    {
        public override string TestSettingCategory { get { return "CSP"; } }
        public ConsoleDynamicMethodFixtureFWLatestCSP()
        {
        }
    }

    public class ConsoleDynamicMethodFixtureFW481 : ConsoleDynamicMethodFixtureFWSpecificVersion
    {
        public ConsoleDynamicMethodFixtureFW481() : base("net481")
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

    public class ConsoleDynamicMethodFixtureCore60 : ConsoleDynamicMethodFixtureCoreSpecificVersion
    {
        public ConsoleDynamicMethodFixtureCore60() : base("net6.0")
        {
        }
    }

    public class ConsoleDynamicMethodFixtureCore80 : ConsoleDynamicMethodFixtureCoreSpecificVersion
    {
        public ConsoleDynamicMethodFixtureCore80() : base("net8.0")
        {
        }
    }

    /// <summary>
    /// Use this fixture to test against the oldest supported .NET version.
    /// If you need to test against a feature that belongs to a specific .net core version, then consider
    /// using one of the existing specific version fixtures, or create a new specific version.
    /// When testing newer .net core preview releases, this targetFramework version should be updated.
    /// </summary>
    public class ConsoleDynamicMethodFixtureCoreOldest : ConsoleDynamicMethodFixtureCore60
    {
        public ConsoleDynamicMethodFixtureCoreOldest()
        {
        }
    }

    /// <summary>
    /// Use this fixture if you don't care about which .net core version the test application should use.
    /// If you need to test against a feature that belongs to a specific .net core version, then consider
    /// using one of the existing specific version fixtures, or create a new specific version.
    /// When testing newer .net core preview releases, this targetFramework version should be updated.
    /// </summary>
    public class ConsoleDynamicMethodFixtureCoreLatest : ConsoleDynamicMethodFixtureCore80
    {
        public ConsoleDynamicMethodFixtureCoreLatest()
        {
        }
    }

    /// <summary>
    /// Use this fixture for High Security Mode tests
    /// </summary>
    public class ConsoleDynamicMethodFixtureCoreLatestHSM : ConsoleDynamicMethodFixtureCore80
    {
        public override string TestSettingCategory { get { return "HSM"; } }
        public ConsoleDynamicMethodFixtureCoreLatestHSM()
        {
        }

    }

    /// <summary>
    /// Use this fixture for Configurable Security Policy tests
    /// </summary>
    public class ConsoleDynamicMethodFixtureCoreLatestCSP : ConsoleDynamicMethodFixtureCore80
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
        public ConsoleDynamicMethodFixtureCoreSpecificVersion(string targetFramework) :
            base(ApplicationDirectoryName,
                ExecutableName,
                targetFramework,
                true,
                DefaultTimeout)
        {
        }
    }

    public abstract class ConsoleDynamicMethodFixture : RemoteApplicationFixture
    {
        protected static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

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

        public void SendCommand(string cmd)
        {
            if (!RemoteApplication.IsRunning)
            {
                throw new Exception($"Remote Process has exited, Cannot execute command: {cmd}");
            }

            RemoteApplication.WriteToStandardInput(cmd);
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
                    SendCommand(cmd);
                }
            });
        }

        public override void ShutdownRemoteApplication()
        {
            try
            {
                RemoteApplication.WriteToStandardInput("exit");
            }
            catch (System.IO.IOException)
            {
                // Starting in .NET 8, writes to a closed pipe throw an IO exception. So we'll just eat it and continue.
            }

            base.ShutdownRemoteApplication();
        }
    }
}
