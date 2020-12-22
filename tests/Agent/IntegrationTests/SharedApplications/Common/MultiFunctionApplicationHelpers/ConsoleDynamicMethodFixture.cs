// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MultiFunctionApplicationHelpers
{

    public class ConsoleDynamicMethodFixtureFW471 : ConsoleDynamicMethodFixtureFWSpecificVersion
    {
        public ConsoleDynamicMethodFixtureFW471() : base("net471")
        {
        }
    }

    public class ConsoleDynamicMethodFixtureFW461 : ConsoleDynamicMethodFixtureFWSpecificVersion
    {
        public ConsoleDynamicMethodFixtureFW461() : base("net461")
        {
        }
    }

    public abstract class ConsoleDynamicMethodFixtureFWSpecificVersion : ConsoleDynamicMethodFixture
    {
        private static readonly string ApplicationDirectoryName = @"ConsoleMultiFunctionApplicationFW";
        private static readonly string ExecutableName = $"{ApplicationDirectoryName}.exe";

        // TODO: is this needed?
        public string IntegrationTestAppPath => RemoteApplication.SourceApplicationsDirectoryPath;

        /// <summary>
        /// Use this .ctor to specify a specific .NET framework version to target.
        /// </summary>
        /// <param name="targetFramework">The framework target to use when running the application. This parameter must match one of the targetFramework values defined in ConsoleMultiFunctionApplicationFW.csproj </param>
        public ConsoleDynamicMethodFixtureFWSpecificVersion(string targetFramework) : base(ApplicationDirectoryName, ExecutableName, targetFramework, false, DefaultTimeout)
        {
        }
    }


    public class ConsoleDynamicMethodFixtureCore22 : ConsoleDynamicMethodFixtureCoreSpecificVersion
    {
        public ConsoleDynamicMethodFixtureCore22() : base("netcoreapp2.2")
        {
        }
    }

    /// <summary>
    /// Use this fixture if you don't care about which .net core version the test application should use.
    /// If you need to test against a feature that belongs to a specific .net core version, then consider
    /// using one of the existing specific version fixtures, or create a new specific version.
    /// When testing newer .net core preview releases, this targetFramework version should be updated.
    /// </summary>
    public class ConsoleDynamicMethodFixtureCoreLatest : ConsoleDynamicMethodFixtureCoreSpecificVersion
    {
        public ConsoleDynamicMethodFixtureCoreLatest() : base("netcoreapp3.0")
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

        protected RemoteConsoleApplication _remoteConsoleApplication => RemoteApplication as RemoteConsoleApplication;

        public ConsoleDynamicMethodFixture SetTimeout(TimeSpan span)
        {
            _remoteConsoleApplication.SetTimeout(span);
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
