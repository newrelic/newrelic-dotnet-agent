// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#if NETFRAMEWORK

using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using NewRelic.Agent.Core.Configuration;
using NUnit.Framework;

namespace NewRelic.Agent.Core.Config
{
    [TestFixture]
    public class ConfigurationLoaderTests
    {
        [Test]
        public void GetWebConfigAppSetting_NonWebApp_ReturnsDefaultSettings()
        {
            var valueWithProvenance = ConfigurationLoader.GetWebConfigAppSetting("foo");
            Assert.Multiple(() =>
            {
                Assert.That(valueWithProvenance.Provenance, Does.Contain("default"));
                Assert.That(valueWithProvenance.Value, Is.Null);
            });
        }

        [Test]
        public void GetWebConfigAppSetting_WebApp_ReturnsSettingsForApp()
        {
            using (var staticMocks = new ConfigurationLoaderStaticMocks())
            {
                var testWebConfiguration = ConfigurationManager.OpenExeConfiguration(null);
                testWebConfiguration.AppSettings.Settings.Add("foo", "bar");

                staticMocks.UseAppDomainAppIdFunc(() => "testAppId");
                staticMocks.UseAppDomainAppVirtualPathFunc(() => "testVirtualPath");
                staticMocks.UseOpenWebConfigurationFunc(_ => testWebConfiguration);

                var valueWithProvenance = ConfigurationLoader.GetWebConfigAppSetting("foo");
                Assert.Multiple(() =>
                {
                    Assert.That(valueWithProvenance.Provenance, Does.Not.Contain("default"));
                    Assert.That(valueWithProvenance.Value, Is.EqualTo("bar"));
                });
            }
        }
        [Test]
        public void GetWebConfigAppSetting_WebApp_ReturnsDefaultSettingsIfSettingNotAvailable()
        {
            using (var staticMocks = new ConfigurationLoaderStaticMocks())
            {
                var testWebConfiguration = ConfigurationManager.OpenExeConfiguration(null);

                staticMocks.UseAppDomainAppIdFunc(() => "testAppId");
                staticMocks.UseAppDomainAppVirtualPathFunc(() => "testVirtualPath");
                staticMocks.UseOpenWebConfigurationFunc(_ => testWebConfiguration);

                var valueWithProvenance = ConfigurationLoader.GetWebConfigAppSetting("foo");
                Assert.Multiple(() =>
                {
                    Assert.That(valueWithProvenance.Provenance, Does.Contain("default"));
                    Assert.That(valueWithProvenance.Value, Is.Null);
                });
            }
        }

        [Test]
        public void GetWebConfigAppSetting_WebApp_ReturnsDefaultSettingsIfExceptionOccurs()
        {
            using (var staticMocks = new ConfigurationLoaderStaticMocks())
            {
                staticMocks.UseAppDomainAppIdFunc(() => "testAppId");
                staticMocks.UseAppDomainAppVirtualPathFunc(() => "testVirtualPath");
                staticMocks.UseOpenWebConfigurationFunc(_ => throw new Exception("Could not load test configuration."));

                var valueWithProvenance = ConfigurationLoader.GetWebConfigAppSetting("foo");
                Assert.Multiple(() =>
                {
                    Assert.That(valueWithProvenance.Provenance, Does.Contain("default"));
                    Assert.That(valueWithProvenance.Value, Is.Null);
                });
            }
        }

        [Test]
        public void GetConfigSetting_NonWebApp_ReturnsConfigurationManagerSetting()
        {
            var valueWithProvenance = ConfigurationLoader.GetConfigSetting("foo");
            Assert.Multiple(() =>
            {
                Assert.That(valueWithProvenance.Provenance, Does.Contain("ConfigurationManager"));
                Assert.That(valueWithProvenance.Value, Is.Null);
            });
        }

        [Test]
        public void GetConfigSetting_WebApp_ReturnsWebAppSetting()
        {
            using (var staticMocks = new ConfigurationLoaderStaticMocks())
            {
                var testWebConfiguration = ConfigurationManager.OpenExeConfiguration(null);
                testWebConfiguration.AppSettings.Settings.Add("foo", "bar");

                staticMocks.UseAppDomainAppIdFunc(() => "testAppId");
                staticMocks.UseAppDomainAppVirtualPathFunc(() => "testVirtualPath");
                staticMocks.UseOpenWebConfigurationFunc(_ => testWebConfiguration);

                var valueWithProvenance = ConfigurationLoader.GetConfigSetting("foo");
                Assert.Multiple(() =>
                {
                    Assert.That(valueWithProvenance.Provenance, Does.Not.Contain("default"));
                    Assert.That(valueWithProvenance.Value, Is.EqualTo("bar"));
                });
            }
        }

        [Test]
        public void GetAgentConfigFileName_ThrowsExceptionWhenNoneFound()
        {
            using (var staticMocks = new ConfigurationLoaderStaticMocks())
            {
                ReplaceNewRelicHomeWithNullIfNecessary(staticMocks);
                var actualException = Assert.Catch<Exception>(() => ConfigurationLoader.GetAgentConfigFileName(), "Expected an exception to be thrown");
                Assert.That(actualException.Message, Does.Contain("Could not find newrelic.config"));
            }
        }

        [Test]
        public void GetAgentConfigFileName_ReturnsConfigFileFromAppConfig()
        {
            using (var staticMocks = new ConfigurationLoaderStaticMocks())
            {
                const string expectedFileName = "filenameFromAppConfig";
                var testWebConfiguration = ConfigurationManager.OpenExeConfiguration(null);
                testWebConfiguration.AppSettings.Settings.Add(Constants.AppSettingsConfigFile, expectedFileName);

                staticMocks.UseAppDomainAppIdFunc(() => "testAppId");
                staticMocks.UseAppDomainAppVirtualPathFunc(() => "testVirtualPath");
                staticMocks.UseOpenWebConfigurationFunc(_ => testWebConfiguration);
                staticMocks.UseFileExistsFunc(_ => true);

                var agentConfigFileName = ConfigurationLoader.GetAgentConfigFileName();

                Assert.That(agentConfigFileName, Is.EqualTo(expectedFileName));
            }
        }
        [Test]
        public void TryGetAgentConfigFileFromAppConfig_ReturnsNullWhenFileDoesNotExist()
        {
            using (var staticMocks = new ConfigurationLoaderStaticMocks())
            {
                const string expectedFileName = "filenameFromAppConfig";
                var testWebConfiguration = ConfigurationManager.OpenExeConfiguration(null);
                testWebConfiguration.AppSettings.Settings.Add(Constants.AppSettingsConfigFile, expectedFileName);

                staticMocks.UseAppDomainAppIdFunc(() => "testAppId");
                staticMocks.UseAppDomainAppVirtualPathFunc(() => "testVirtualPath");
                staticMocks.UseOpenWebConfigurationFunc(_ => testWebConfiguration);
                staticMocks.UseFileExistsFunc(_ => false);

                var actualException = Assert.Catch<Exception>(() => ConfigurationLoader.GetAgentConfigFileName(), "Expected an exception to be thrown");
                Assert.That(actualException.Message, Does.Contain("Could not find newrelic.config"));
            }
        }

        [Test]
        public void TryGetAgentConfigFileFromAppConfig_ReturnsNullOnException()
        {
            using (var staticMocks = new ConfigurationLoaderStaticMocks())
            {
                const string expectedFileName = "filenameFromAppConfig";
                var testWebConfiguration = ConfigurationManager.OpenExeConfiguration(null);
                testWebConfiguration.AppSettings.Settings.Add(Constants.AppSettingsConfigFile, expectedFileName);

                staticMocks.UseAppDomainAppIdFunc(() => "testAppId");
                staticMocks.UseAppDomainAppVirtualPathFunc(() => "testVirtualPath");
                staticMocks.UseOpenWebConfigurationFunc(_ => testWebConfiguration);
                staticMocks.UseFileExistsFunc(_ => throw new Exception("Exception from FileExists call"));

                var actualException = Assert.Catch<Exception>(() => ConfigurationLoader.GetAgentConfigFileName(), "Expected an exception to be thrown");
                Assert.That(actualException.Message, Does.Contain("Could not find newrelic.config"));
            }
        }

        [Test]
        public void TryGetAgentConfigFileFromAppRoot_ReturnsNullIfNoAppPath()
        {
            using (var staticMocks = new ConfigurationLoaderStaticMocks())
            {
                ReplaceNewRelicHomeWithNullIfNecessary(staticMocks);
                staticMocks.UseAppDomainAppVirtualPathFunc(() => "testVirtualPath");
                var actualException = Assert.Catch<Exception>(() => ConfigurationLoader.GetAgentConfigFileName(), "Expected an exception to be thrown");
                Assert.That(actualException.Message, Does.Contain("Could not find newrelic.config"));
            }
        }

        [Test]
        public void TryGetAgentConfigFileFromAppRoot_ReturnsNullIfFileDoesNotExist()
        {
            using (var staticMocks = new ConfigurationLoaderStaticMocks())
            {
                staticMocks.UseAppDomainAppVirtualPathFunc(() => "testVirtualPath");
                staticMocks.UseAppDomainAppPathFunc(() => "testPath");
                staticMocks.UseFileExistsFunc(_ => false);

                var actualException = Assert.Catch<Exception>(() => ConfigurationLoader.GetAgentConfigFileName(), "Expected an exception to be thrown");
                Assert.That(actualException.Message, Does.Contain("Could not find newrelic.config"));
            }
        }

        [Test]
        public void TryGetAgentConfigFileFromAppRoot_ReturnsNullIfExceptionOccurs()
        {
            using (var staticMocks = new ConfigurationLoaderStaticMocks())
            {
                staticMocks.UseAppDomainAppVirtualPathFunc(() => "testVirtualPath");
                staticMocks.UseAppDomainAppPathFunc(() => "testPath");
                staticMocks.UseFileExistsFunc(_ => throw new Exception("Exception from FileExists call"));

                var actualException = Assert.Catch<Exception>(() => ConfigurationLoader.GetAgentConfigFileName(), "Expected an exception to be thrown");
                Assert.That(actualException.Message, Does.Contain("Could not find newrelic.config"));
            }
        }

        [Test]
        public void TryGetAgentConfigFileFromAppRoot_ReturnsFileNameIfExists()
        {
            using (var staticMocks = new ConfigurationLoaderStaticMocks())
            {
                staticMocks.UseAppDomainAppVirtualPathFunc(() => "testVirtualPath");
                staticMocks.UseAppDomainAppPathFunc(() => "testPath");
                staticMocks.UseFileExistsFunc(f => f.Contains("testPath"));


                var agentConfigFileName = ConfigurationLoader.GetAgentConfigFileName();

                Assert.That(agentConfigFileName, Is.EqualTo("testPath\\newrelic.config"));
            }
        }

        [Test]
        public void TryGetAgentConfigFileFromExecutionPath_ReturnsNullNoDirectoryForProcess()
        {
            using (var staticMocks = new ConfigurationLoaderStaticMocks())
            {
                ReplaceNewRelicHomeWithNullIfNecessary(staticMocks);
                staticMocks.UsePathGetDirectoryNameFunc(_ => null);

                var actualException = Assert.Catch<Exception>(() => ConfigurationLoader.GetAgentConfigFileName(), "Expected an exception to be thrown");
                Assert.That(actualException.Message, Does.Contain("Could not find newrelic.config"));
            }
        }

        [Test]
        public void TryGetAgentConfigFileFromExecutionPath_ReturnsNullWhenFileNotFound()
        {
            using (var staticMocks = new ConfigurationLoaderStaticMocks())
            {
                staticMocks.UsePathGetDirectoryNameFunc(_ => "executionPath");
                staticMocks.UseFileExistsFunc(_ => false);

                var actualException = Assert.Catch<Exception>(() => ConfigurationLoader.GetAgentConfigFileName(), "Expected an exception to be thrown");
                Assert.That(actualException.Message, Does.Contain("Could not find newrelic.config"));
            }
        }

        [Test]
        public void TryGetAgentConfigFileFromExecutionPath_ReturnsNullIfExceptionIsThrown()
        {
            using (var staticMocks = new ConfigurationLoaderStaticMocks())
            {
                staticMocks.UsePathGetDirectoryNameFunc(_ => "executionPath");
                staticMocks.UseFileExistsFunc(f => f.Contains("executionPath") ? throw new Exception("Exception from FileExists") : false);

                var actualException = Assert.Catch<Exception>(() => ConfigurationLoader.GetAgentConfigFileName(), "Expected an exception to be thrown");
                Assert.That(actualException.Message, Does.Contain("Could not find newrelic.config"));
            }
        }

        [Test]
        public void TryGetAgentConfigFileFromExecutionPath_ReturnsFileNameIfExists()
        {
            using (var staticMocks = new ConfigurationLoaderStaticMocks())
            {
                staticMocks.UsePathGetDirectoryNameFunc(_ => "executionPath");
                staticMocks.UseFileExistsFunc(f => f.Contains("executionPath"));

                var agentConfigFileName = ConfigurationLoader.GetAgentConfigFileName();

                Assert.That(agentConfigFileName, Is.EqualTo("executionPath\\newrelic.config"));
            }
        }

        [Test]
        public void TryGetAgentConfigFileFromNewRelicHome_ReturnsNullIfConfigNotFound()
        {
            using (var staticMocks = new ConfigurationLoaderStaticMocks())
            {
                staticMocks.UseGetNewRelicHome(() => "newRelicHome");
                staticMocks.UseFileExistsFunc(_ => false);

                var actualException = Assert.Catch<Exception>(() => ConfigurationLoader.GetAgentConfigFileName(), "Expected an exception to be thrown");
                Assert.That(actualException.Message, Does.Contain("Could not find newrelic.config"));
            }
        }

        [Test]
        public void TryGetAgentConfigFileFromNewRelicHome_ReturnsNullIfExceptionIsThrown()
        {
            using (var staticMocks = new ConfigurationLoaderStaticMocks())
            {
                staticMocks.UseGetNewRelicHome(() => "newRelicHome");
                staticMocks.UseFileExistsFunc(f => f.Contains("newRelicHome") ? throw new Exception("Exception from FileExists") : false);

                var actualException = Assert.Catch<Exception>(() => ConfigurationLoader.GetAgentConfigFileName(), "Expected an exception to be thrown");
                Assert.That(actualException.Message, Does.Contain("Could not find newrelic.config"));
            }
        }

        [Test]
        public void TryGetAgentConfigFileFromNewRelicHome_ReturnsFileNameIfExists()
        {
            using (var staticMocks = new ConfigurationLoaderStaticMocks())
            {
                staticMocks.UseGetNewRelicHome(() => "newRelicHome");
                staticMocks.UseFileExistsFunc(f => f.Contains("newRelicHome"));

                var agentConfigFileName = ConfigurationLoader.GetAgentConfigFileName();

                Assert.That(agentConfigFileName, Is.EqualTo("newRelicHome\\newrelic.config"));
            }
        }

        [Test]
        public void TryGetAgentConfigFileFromCurrentDirectory_ReturnsNullIfConfigNotFound()
        {
            using (var staticMocks = new ConfigurationLoaderStaticMocks())
            {
                staticMocks.UseFileExistsFunc(f => false);

                var actualException = Assert.Catch<Exception>(() => ConfigurationLoader.GetAgentConfigFileName(), "Expected an exception to be thrown");
                Assert.That(actualException.Message, Does.Contain("Could not find newrelic.config"));
            }
        }

        [Test]
        public void TryGetAgentConfigFileFromCurrentDirectory_ReturnsNullIfExceptionIsThrown()
        {
            using (var staticMocks = new ConfigurationLoaderStaticMocks())
            {
                staticMocks.UseFileExistsFunc(f => f == "newrelic.config" ? throw new Exception("Exception from FileExists") : false);

                var actualException = Assert.Catch<Exception>(() => ConfigurationLoader.GetAgentConfigFileName(), "Expected an exception to be thrown");
                Assert.That(actualException.Message, Does.Contain("Could not find newrelic.config"));
            }
        }

        [Test]
        public void TryGetAgentConfigFileFromCurrentDirectory_ReturnsFileNameIfExists()
        {
            using (var staticMocks = new ConfigurationLoaderStaticMocks())
            {
                staticMocks.UseFileExistsFunc(f => f == "newrelic.config");

                var agentConfigFileName = ConfigurationLoader.GetAgentConfigFileName();

                Assert.That(agentConfigFileName, Is.EqualTo("newrelic.config"));
            }
        }

        [Test]
        public void GetConfigurationFilePath_ReturnsFileNameIfFound()
        {
            using (var staticMocks = new ConfigurationLoaderStaticMocks())
            {
                staticMocks.UseFileExistsFunc(f => true);

                var fileName = ConfigurationLoader.GetConfigurationFilePath("homeDirectory");

                Assert.That(fileName, Is.EqualTo("homeDirectory\\newrelic.config"));
            }
        }

        [Test]
        public void GetConfigurationFilePath_ThrowsExceptionWhenFileNotFound()
        {
            using (var staticMocks = new ConfigurationLoaderStaticMocks())
            {
                staticMocks.UseFileExistsFunc(_ => false);

                var actualException = Assert.Catch<Exception>(() => ConfigurationLoader.GetConfigurationFilePath("homeDirectory"), "Expected an exception to be thrown");
                Assert.That(actualException.Message, Does.StartWith("Could not find the config file in the new relic home directory"));
            }
        }

        [Test]
        public void Initialize_ThrowsExceptionWhenFileDoesNotExist()
        {
            var fileRequestCount = 0;
            using (var staticMocks = new ConfigurationLoaderStaticMocks())
            {
                staticMocks.UseGetNewRelicHome(() => "newrelichome");
                staticMocks.UseFileExistsFunc(FailAfterFirstAttemptForFile);

                var actualException = Assert.Catch<ConfigurationLoaderException>(() => ConfigurationLoader.Initialize(), "Expected an exception to be thrown");
                Assert.That(actualException.Message, Does.StartWith("An error occurred reading the New Relic Agent configuration file"));
            }

            bool FailAfterFirstAttemptForFile(string fileName)
            {
                if (fileRequestCount == 0 && fileName == "newrelichome\\newrelic.config")
                {
                    fileRequestCount++;
                    return true;
                }

                return false;
            }
        }

        [Test]
        public void Initialize_ThrowsExceptionWhenThereArePermissionsProblems()
        {
            var fileRequestCount = 0;
            using (var staticMocks = new ConfigurationLoaderStaticMocks())
            {
                staticMocks.UseGetNewRelicHome(() => "newrelichome");
                staticMocks.UseFileExistsFunc(FailAfterFirstAttemptForFile);

                var actualException = Assert.Catch<ConfigurationLoaderException>(() => ConfigurationLoader.Initialize(), "Expected an exception to be thrown");
                Assert.That(actualException.Message, Does.StartWith("Unable to access the New Relic Agent configuration file"));
            }

            bool FailAfterFirstAttemptForFile(string fileName)
            {
                if (fileName == "newrelichome\\newrelic.config")
                {
                    fileRequestCount++;
                    if (fileRequestCount > 1)
                    {
                        throw new UnauthorizedAccessException();
                    }
                    return true;
                }

                return false;
            }
        }

        [Test]
        public void Initialize_ThrowsExceptionWhenThereIsAFileNotFoundException()
        {
            var fileRequestCount = 0;
            using (var staticMocks = new ConfigurationLoaderStaticMocks())
            {
                staticMocks.UseGetNewRelicHome(() => "newrelichome");
                staticMocks.UseFileExistsFunc(FailAfterFirstAttemptForFile);

                var actualException = Assert.Catch<ConfigurationLoaderException>(() => ConfigurationLoader.Initialize(), "Expected an exception to be thrown");
                Assert.That(actualException.Message, Does.StartWith("Unable to find the New Relic Agent configuration file"));
            }

            bool FailAfterFirstAttemptForFile(string fileName)
            {
                if (fileName == "newrelichome\\newrelic.config")
                {
                    fileRequestCount++;
                    if (fileRequestCount > 1)
                    {
                        throw new FileNotFoundException();
                    }
                    return true;
                }

                return false;
            }
        }

        [Test]
        public void Initialize_GetLocalConfigWithFileName()
        {
            using (var staticMocks = new ConfigurationLoaderStaticMocks())
            using (var configFile = new TempNewRelicConfigFile(GetDefaultNewRelicConfigContents()))
            {
                staticMocks.UseGetNewRelicHome(() => configFile.FilePath);

                var parsedConfig = ConfigurationLoader.Initialize();

                Assert.That(ConfigurationLoader.BootstrapConfig.ConfigurationFileName, Is.EqualTo(configFile.FileName));
            }
        }

        [Test]
        public void InitializeFromXml_DoesNotThrowWhenXsdValidationThrows()
        {
            var configXml = GetDefaultNewRelicConfigContents();
            Func<string> schemaFunc = () => throw new Exception("Failed to get schema");

            Assert.DoesNotThrow(() => ConfigurationLoader.InitializeFromXml(configXml, schemaFunc));
        }

        [Test]
        public void InitializeFromXml_RemovesOldApdexAttribute()
        {
            var configXml = @"<?xml version=""1.0""?>
<configuration xmlns=""urn:newrelic-config"">
    <application apdexT=""1.0"">
    </application>
</configuration>
";
            Func<string> schemaFunc = () => string.Empty;

            Assert.DoesNotThrow(() => ConfigurationLoader.InitializeFromXml(configXml, schemaFunc));
        }

        [Test]
        public void InitializeFromXml_RemovesOldSslAttribute()
        {
            var configXml = @"<?xml version=""1.0""?>
<configuration xmlns=""urn:newrelic-config"">
    <service ssl=""false"">
    </service>
</configuration>
";
            Func<string> schemaFunc = () => string.Empty;

            Assert.DoesNotThrow(() => ConfigurationLoader.InitializeFromXml(configXml, schemaFunc));
        }

        private static string GetDefaultNewRelicConfigContents()
        {
            return @"<?xml version=""1.0""?>
<!-- Copyright (c) 2008-2020 New Relic, Inc.  All rights reserved. -->
<!-- For more information see: https://docs.newrelic.com/docs/agents/net-agent/configuration/net-agent-configuration/ -->
<configuration xmlns=""urn:newrelic-config"" agentEnabled=""true"">
	<service licenseKey=""REPLACE_WITH_LICENSE_KEY"" />
	<application>
		<name>My Application</name>
	</application>
	<log level=""info"" />
	<allowAllHeaders enabled=""true"" />
	<attributes enabled=""true"">
		<exclude>request.headers.cookie</exclude>
		<exclude>request.headers.authorization</exclude>
		<exclude>request.headers.proxy-authorization</exclude>
		<exclude>request.headers.x-*</exclude>

		<include>request.headers.*</include>
	</attributes>
	<transactionTracer enabled=""true""
		transactionThreshold=""apdex_f""
		stackTraceThreshold=""500""
		recordSql=""obfuscated""
		explainEnabled=""false""
		explainThreshold=""500"" />
	<distributedTracing enabled=""true"" />
	<errorCollector enabled=""true"">
		<ignoreClasses>
			<errorClass>System.IO.FileNotFoundException</errorClass>
			<errorClass>System.Threading.ThreadAbortException</errorClass>
		</ignoreClasses>
		<ignoreStatusCodes>
			<code>401</code>
			<code>404</code>
		</ignoreStatusCodes>
	</errorCollector>
	<browserMonitoring autoInstrument=""true"" />
	<threadProfiling>
		<ignoreMethod>System.Threading.WaitHandle:InternalWaitOne</ignoreMethod>
		<ignoreMethod>System.Threading.WaitHandle:WaitAny</ignoreMethod>
	</threadProfiling>
</configuration>
";
        }

        private static void ReplaceNewRelicHomeWithNullIfNecessary(ConfigurationLoaderStaticMocks mocks)
        {
            if (!string.IsNullOrEmpty(AgentInstallConfiguration.NewRelicHome))
            {
                mocks.UseGetNewRelicHome(() => null);
            }
        }

        private class ConfigurationLoaderStaticMocks : IDisposable
        {
            private readonly Func<string> _originalGetAppDomainAppId;
            private readonly Func<string> _originalGetAppDomainAppVirtualPath;
            private readonly Func<string> _originalGetAppDomainAppPath;
            private readonly Func<string, System.Configuration.Configuration> _originalOpenWebConfiguration;
            private readonly Func<string, bool> _originalFileExists;
            private readonly Func<string, string> _originalPathGetDirectoryName;
            private readonly Func<string> _originalGetNewRelicHome;

            public ConfigurationLoaderStaticMocks()
            {
                _originalGetAppDomainAppId = ConfigurationLoader.GetAppDomainAppId;
                _originalGetAppDomainAppVirtualPath = ConfigurationLoader.GetAppDomainAppVirtualPath;
                _originalGetAppDomainAppPath = ConfigurationLoader.GetAppDomainAppPath;
                _originalOpenWebConfiguration = ConfigurationLoader.OpenWebConfiguration;
                _originalFileExists = ConfigurationLoader.FileExists;
                _originalPathGetDirectoryName = ConfigurationLoader.PathGetDirectoryName;
                _originalGetNewRelicHome = ConfigurationLoader.GetNewRelicHome;
            }

            public void UseAppDomainAppIdFunc(Func<string> appDomainAppIdFunc)
            {
                ConfigurationLoader.GetAppDomainAppId = appDomainAppIdFunc;
            }

            public void UseAppDomainAppVirtualPathFunc(Func<string> appDomainAppVirtualPathFunc)
            {
                ConfigurationLoader.GetAppDomainAppVirtualPath = appDomainAppVirtualPathFunc;
            }

            public void UseAppDomainAppPathFunc(Func<string> appDomainAppPathFunc)
            {
                ConfigurationLoader.GetAppDomainAppPath = appDomainAppPathFunc;
            }

            public void UseOpenWebConfigurationFunc(Func<string, System.Configuration.Configuration> openWebConfigurationFunc)
            {
                ConfigurationLoader.OpenWebConfiguration = openWebConfigurationFunc;
            }

            public void UseFileExistsFunc(Func<string, bool> fileExistsFunc)
            {
                ConfigurationLoader.FileExists = fileExistsFunc;
            }

            public void UsePathGetDirectoryNameFunc(Func<string, string> pathGetDirectoryNameFunc)
            {
                ConfigurationLoader.PathGetDirectoryName = pathGetDirectoryNameFunc;
            }

            public void UseGetNewRelicHome(Func<string> getNewRelicHome)
            {
                ConfigurationLoader.GetNewRelicHome = getNewRelicHome;
            }

            public void Dispose()
            {
                ConfigurationLoader.GetAppDomainAppId = _originalGetAppDomainAppId;
                ConfigurationLoader.GetAppDomainAppVirtualPath = _originalGetAppDomainAppVirtualPath;
                ConfigurationLoader.GetAppDomainAppPath = _originalGetAppDomainAppPath;
                ConfigurationLoader.OpenWebConfiguration = _originalOpenWebConfiguration;
                ConfigurationLoader.FileExists = _originalFileExists;
                ConfigurationLoader.PathGetDirectoryName = _originalPathGetDirectoryName;
                ConfigurationLoader.GetNewRelicHome = _originalGetNewRelicHome;
            }
        }

        public class TempNewRelicConfigFile : IDisposable
        {
            public readonly string FilePath;

            public string FileName { get; private set; }

            public TempNewRelicConfigFile(string fileContents)
            {
                FilePath = Path.GetTempPath();
                var fileName = Path.Combine(FilePath, "newrelic.config");
                File.WriteAllText(fileName, fileContents);
                FileName = fileName;
            }

            public void Dispose()
            {
                if (FileName != null)
                {
                    File.Delete(FileName);
                    FileName = null;
                }
            }
        }
    }
}
#endif
