using System;
using System.Configuration;
using System.IO;
using System.Reflection;

namespace FunctionalTests.Helpers
{
    public static class Settings
    {
        private static string _agentVersion = ConfigurationManager.AppSettings["AgentVersion"];
        public static string AgentVersion { get { return _agentVersion; } }

        private static string[] _remoteServers = ConfigurationManager.AppSettings["RemoteServers"].Split(',');
        public static string[] RemoteServers { get { return _remoteServers; } }

        private static string _licenseKey = IsDeveloperMode
            ? System.Environment.GetEnvironmentVariable("NEWRELIC_LICENSEKEY")
            : ConfigurationManager.AppSettings["LicenseKey"];
        public static string LicenseKey { get { return _licenseKey; } }

        private static string _environment = System.Environment.GetEnvironmentVariable("NEWRELIC_FUNCTIONAL_TEST_ENV") ?? ConfigurationManager.AppSettings["Environment"];
        public static Enumerations.EnvironmentSetting Environment
        {
            get
            {
                if (string.IsNullOrEmpty(_environment))
                {
                    _environment = System.Environment.GetEnvironmentVariable("NEWRELIC_FUNCTIONAL_TEST_ENV") ?? ConfigurationManager.AppSettings["Environment"];
                }
                return (Enumerations.EnvironmentSetting)Enum.Parse(typeof(Enumerations.EnvironmentSetting), _environment);
            }
        }

        public static bool IsDeveloperMode { get { return Settings.Environment == Enumerations.EnvironmentSetting.Developer; } }

        private static string _workspace = string.Empty;
        public static string Workspace
        {
            get
            {
                //\Tests\Agent\MsiInstallerTests\bin\Release
                //\Tests\Agent\MsiInstallerTests\bin
                //\Tests\Agent\MsiInstallerTests
                //\Tests\Agent
                //\Tests
                //\
                if (string.IsNullOrEmpty(_workspace))
                {
                    var leaf = new DirectoryInfo(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)); // \Tests\Agent\MsiInstallerTests\bin\Release
                    var root = leaf.Parent.Parent.Parent.Parent.Parent;
                    _workspace = root.FullName;
                }

                return _workspace.TrimEnd('\\');
            }
        }
    }
}
