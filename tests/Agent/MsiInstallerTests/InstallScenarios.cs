/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Xml;
using FunctionalTests.Helpers;
using Microsoft.Win32;
using NUnit.Framework;

namespace FunctionalTests
{
    [TestFixture]
    [Category("Install")]
    [Description("Verifies various scenarios with the agent installer.")]
    class InstallScenarios
    {
        private TestServer _tServer = new TestServer(Settings.RemoteServers[0]);

        [SetUp]
        public void SetUp()
        {
            _tServer.IISCommand("Stop");
            _tServer.RunCleanUninstall(testName: nameof(SetUp));
            ComponentManager.CleanComponents(_tServer, testName: nameof(SetUp));
        }

        [Test]
        [Description("Verifies the license key is correctly set when the user passes 'NR_LICENSE_KEY=[key]' via the command line without using ADDLOCAL.")]
        public void LicenseKeySet_DefaultInstall()
        {
            _tServer.CommandLineInstall(Settings.LicenseKey, null, testName: nameof(LicenseKeySet_DefaultInstall));
            ComponentManager.TruncateComponents(_tServer);

            // Check the value in the 'newrelic.config' for license
            var config = _tServer.LoadXmlFile(String.Format("{0}\\newrelic.config", _tServer.DataPath));
            var nsmgr = new XmlNamespaceManager(config.NameTable);
            nsmgr.AddNamespace("ns", "urn:newrelic-config");

            // Verify the license was correctly set by the installer
            Assert.AreEqual(Settings.LicenseKey, config.SelectSingleNode("//ns:service/@licenseKey", nsmgr).Value);
        }

        [Test]
        [Description("Verifies the license key is correctly set when the user passes 'NR_LICENSE_KEY=[key]' via the command line to install only Framework.")]
        public void LicenseKeySet_FrameworkOnly()
        {
            _tServer.CommandLineInstall(Settings.LicenseKey, new List<Enumerations.InstallFeatures>() { Enumerations.InstallFeatures.StartMenuShortcuts, Enumerations.InstallFeatures.InstrumentAllNETFramework, Enumerations.InstallFeatures.NETFrameworkSupport, Enumerations.InstallFeatures.ASPNETTools }, testName: nameof(LicenseKeySet_FrameworkOnly));
            ComponentManager.TruncateComponents(_tServer);

            // Check the value in the 'newrelic.config' for license
            var config = _tServer.LoadXmlFile(String.Format("{0}\\newrelic.config", _tServer.DataPath));
            var nsmgr = new XmlNamespaceManager(config.NameTable);
            nsmgr.AddNamespace("ns", "urn:newrelic-config");

            // Verify the license was correctly set by the installer
            Assert.AreEqual(Settings.LicenseKey, config.SelectSingleNode("//ns:service/@licenseKey", nsmgr).Value);
        }

        [Test]
        [Description("Verifies the license key is correctly set when the user passes 'NR_LICENSE_KEY=[key]' via the command line to install only Core.")]
        public void LicenseKeySet_CoreOnly()
        {
            _tServer.CommandLineInstall(Settings.LicenseKey, new List<Enumerations.InstallFeatures>() { Enumerations.InstallFeatures.StartMenuShortcuts, Enumerations.InstallFeatures.NETCoreSupport, Enumerations.InstallFeatures.ASPNETTools }, testName: nameof(LicenseKeySet_CoreOnly));
            ComponentManager.TruncateComponents(_tServer);

            // Check the value in the 'newrelic.config' for license
            var config = _tServer.LoadXmlFile(String.Format("{0}\\newrelic.config", _tServer.DataPath));
            var nsmgr = new XmlNamespaceManager(config.NameTable);
            nsmgr.AddNamespace("ns", "urn:newrelic-config");

            // Verify the license was correctly set by the installer
            Assert.AreEqual(Settings.LicenseKey, config.SelectSingleNode("//ns:service/@licenseKey", nsmgr).Value);
        }

        [Test]
        [Description("Verifies the 'COR_ENABLE_PROFILING', 'COR_PROFILER', and 'NEWRELIC_INSTALL_PATH' environment variables are set when the 'AllAppsEnvironmentFeature' is selected.")]
        public void EnvironmentVariablesSetWhenAllAppsFeatureSelected()
        {
            _tServer.CommandLineInstall(Settings.LicenseKey, new List<Enumerations.InstallFeatures>() { Enumerations.InstallFeatures.StartMenuShortcuts, Enumerations.InstallFeatures.InstrumentAllNETFramework, Enumerations.InstallFeatures.NETFrameworkSupport, Enumerations.InstallFeatures.ASPNETTools }, testName: nameof(EnvironmentVariablesSetWhenAllAppsFeatureSelected));
            ComponentManager.TruncateComponents(_tServer);

            // Verify the 'COR_ENABLE_PROFILING', 'COR_PROFILER' and 'NEWRELIC_INSTALL_PATH' environment variables were set
            Assert.AreEqual("1", _tServer.FetchEnvironmentVariableValue("COR_ENABLE_PROFILING"));
            Assert.AreEqual("{71DA0A04-7777-4EC6-9643-7D28B46A8A41}", _tServer.FetchEnvironmentVariableValue("COR_PROFILER"));
            Assert.AreEqual(@"C:\Program Files\New Relic\.NET Agent\", _tServer.FetchEnvironmentVariableValue("NEWRELIC_INSTALL_PATH"));
        }

        [Test]
        [Description("Verifies that the Environment values are removed from the WAS and W3SVC registry keys on uninstall. Covers both Framework and Core.")]
        public void EnvironmentRemovedFromW3SVCAndWASOnUninstall()
        {
            _tServer.CommandLineInstall(Settings.LicenseKey, testName: nameof(EnvironmentRemovedFromW3SVCAndWASOnUninstall));
            ComponentManager.TruncateComponents(_tServer);
            _tServer.CommandLineUninstall(testName: nameof(EnvironmentRemovedFromW3SVCAndWASOnUninstall));

            // Verify the values have been removed
            Assert.IsFalse(_tServer.RegistryKeyValueExists(RegistryHive.LocalMachine, @"SYSTEM\CurrentControlSet\Services\W3SVC", "Environment"));
            Assert.IsFalse(_tServer.RegistryKeyValueExists(RegistryHive.LocalMachine, @"SYSTEM\CurrentControlSet\Services\WAS", "Environment"));
            ComponentManager.CleanComponents(_tServer, testName: nameof(EnvironmentRemovedFromW3SVCAndWASOnUninstall));
        }

        [Test]
        [Description("Verifies that all Framework and Core environment variables are removed on uninstall.")]
        public void EnvironmentVariablesRemovedOnUninstall_DefaultInstall()
        {
            _tServer.CommandLineInstall(Settings.LicenseKey, new List<Enumerations.InstallFeatures>() { Enumerations.InstallFeatures.StartMenuShortcuts, Enumerations.InstallFeatures.InstrumentAllNETFramework, Enumerations.InstallFeatures.NETFrameworkSupport, Enumerations.InstallFeatures.ASPNETTools, Enumerations.InstallFeatures.NETCoreSupport }, testName: nameof(EnvironmentVariablesRemovedOnUninstall_DefaultInstall));
            ComponentManager.TruncateComponents(_tServer);
            _tServer.CommandLineUninstall(testName: nameof(EnvironmentVariablesRemovedOnUninstall_DefaultInstall));

            // Verify the 'COR_ENABLE_PROFILING', 'COR_PROFILER', 'COR_PROFILER_PATH', and 'NEWRELIC_HOME' environment variables were removed
            Assert.IsFalse(_tServer.EnvironmentVariableExists("COR_ENABLE_PROFILING"));
            Assert.IsFalse(_tServer.EnvironmentVariableExists("COR_PROFILER"));
            Assert.IsFalse(_tServer.EnvironmentVariableExists("COR_PROFILER_PATH"));
            Assert.IsFalse(_tServer.EnvironmentVariableExists("NEWRELIC_HOME"));
            Assert.IsFalse(_tServer.EnvironmentVariableExists("NEWRELIC_INSTALL_PATH"));
            ComponentManager.CleanComponents(_tServer, testName: nameof(EnvironmentVariablesRemovedOnUninstall_DefaultInstall));
        }

        [Test]
        [Description("Verifies that all Framework environment variables are removed on uninstall.")]
        public void EnvironmentVariablesRemovedOnUninstall_Framework()
        {
            _tServer.CommandLineInstall(Settings.LicenseKey, new List<Enumerations.InstallFeatures>() { Enumerations.InstallFeatures.StartMenuShortcuts, Enumerations.InstallFeatures.InstrumentAllNETFramework, Enumerations.InstallFeatures.NETFrameworkSupport, Enumerations.InstallFeatures.ASPNETTools }, testName: nameof(EnvironmentVariablesRemovedOnUninstall_Framework));
            ComponentManager.TruncateComponents(_tServer);
            _tServer.CommandLineUninstall(testName: nameof(EnvironmentVariablesRemovedOnUninstall_Framework));

            // Verify the 'COR_ENABLE_PROFILING', 'COR_PROFILER', 'COR_PROFILER_PATH', and 'NEWRELIC_HOME' environment variables were removed
            Assert.IsFalse(_tServer.EnvironmentVariableExists("COR_ENABLE_PROFILING"));
            Assert.IsFalse(_tServer.EnvironmentVariableExists("COR_PROFILER"));
            Assert.IsFalse(_tServer.EnvironmentVariableExists("COR_PROFILER_PATH"));
            Assert.IsFalse(_tServer.EnvironmentVariableExists("NEWRELIC_HOME"));
            Assert.IsFalse(_tServer.EnvironmentVariableExists("NEWRELIC_INSTALL_PATH"));
            ComponentManager.CleanComponents(_tServer, testName: nameof(EnvironmentVariablesRemovedOnUninstall_Framework));
        }

        [Test]
        [Description("Verifies that all Core environment variables are removed on uninstall.")]
        public void EnvironmentVariablesRemovedOnUninstall_Core()
        {
            _tServer.CommandLineInstall(Settings.LicenseKey, new List<Enumerations.InstallFeatures>() { Enumerations.InstallFeatures.StartMenuShortcuts, Enumerations.InstallFeatures.NETCoreSupport, Enumerations.InstallFeatures.ASPNETTools }, testName: nameof(EnvironmentVariablesRemovedOnUninstall_Core));
            ComponentManager.TruncateComponents(_tServer);
            _tServer.CommandLineUninstall(testName: nameof(EnvironmentVariablesRemovedOnUninstall_Core));

            // Verify the CORECLR_PROFILER', 'CORECLR_PROFILER_PATH', and 'CORECLR_NEWRELIC_HOME' environment variables were removed
            Assert.IsFalse(_tServer.EnvironmentVariableExists("CORECLR_PROFILER"));
            Assert.IsFalse(_tServer.EnvironmentVariableExists("CORECLR_PROFILER_PATH"));
            Assert.IsFalse(_tServer.EnvironmentVariableExists("CORECLR_NEWRELIC_HOME"));
            Assert.IsFalse(_tServer.EnvironmentVariableExists("CORECLR_NEWRELIC_INSTALL_PATH"));
            ComponentManager.CleanComponents(_tServer, testName: nameof(EnvironmentVariablesRemovedOnUninstall_Core));
        }

        [Test]
        [Description("Verifies the 'Logs' and 'Extensions' folders are not removed on uninstall. Covers Core and Framework.")]
        public void logsAndExtensionsRemainAfterUninstall()
        {
            _tServer.CommandLineInstall(Settings.LicenseKey, testName: nameof(logsAndExtensionsRemainAfterUninstall));
            ComponentManager.TruncateComponents(_tServer);

            // Create folders, files in the 'Logs' and 'Extensions' folders
            FileOperations.CreateFileOrDirectory(_tServer.MgmtScope, @"C:\ProgramData\New Relic\.NET Agent\Logs\Logs.txt");
            FileOperations.CreateFileOrDirectory(_tServer.MgmtScope, @"C:\ProgramData\New Relic\.NET Agent\Logs\LogsSubdirectory", true);
            FileOperations.CreateFileOrDirectory(_tServer.MgmtScope, @"C:\ProgramData\New Relic\.NET Agent\Logs\LogsSubdirectory\LogsSubdirectory.txt");
            FileOperations.CreateFileOrDirectory(_tServer.MgmtScope, @"C:\ProgramData\New Relic\.NET Agent\Extensions\Extensions.txt");
            FileOperations.CreateFileOrDirectory(_tServer.MgmtScope, @"C:\ProgramData\New Relic\.NET Agent\Extensions\ExtensionsSubdirectory", true);
            FileOperations.CreateFileOrDirectory(_tServer.MgmtScope, @"C:\ProgramData\New Relic\.NET Agent\Extensions\ExtensionsSubdirectory\ExtensionsSubdirectory.txt");
            _tServer.CommandLineUninstall(testName: nameof(logsAndExtensionsRemainAfterUninstall));

            // Verify the 'Logs' and 'Extensions' subfolders and files exist
            Assert.IsTrue(FileOperations.FileOrDirectoryExists(_tServer.MgmtScope, @"C:\\ProgramData\\New Relic\\.NET Agent\\Logs\\LogsSubdirectory\\LogsSubdirectory.txt"));
            Assert.IsTrue(FileOperations.FileOrDirectoryExists(_tServer.MgmtScope, @"C:\\ProgramData\\New Relic\\.NET Agent\\Logs\\Logs.txt"));
            Assert.IsTrue(FileOperations.FileOrDirectoryExists(_tServer.MgmtScope, @"C:\\ProgramData\\New Relic\\.NET Agent\\Extensions\\ExtensionsSubdirectory\\ExtensionsSubdirectory.txt"));
            Assert.IsTrue(FileOperations.FileOrDirectoryExists(_tServer.MgmtScope, @"C:\\ProgramData\\New Relic\\.NET Agent\\Extensions\\Extensions.txt"));
        }

        [Test]
        [Description("Verifies a repair install restores the API and All .NET option components while leaving the Logs, Exten. Covers Core and Framework.")]
        public void RepairRestoresAPIAndAllAppsLeavesExtensionsLogsConfig()
        {
            _tServer.CommandLineInstall(licenseKey: Settings.LicenseKey, allFeatures: true, testName: nameof(RepairRestoresAPIAndAllAppsLeavesExtensionsLogsConfig));
            ComponentManager.TruncateComponents(_tServer);

            // Delete the API dll
            FileOperations.DeleteFileOrDirectory(_tServer.MgmtScope, @"C:\\Program Files\\New Relic\\.NET Agent\\netframework\\NewRelic.Api.Agent.dll");
            FileOperations.DeleteFileOrDirectory(_tServer.MgmtScope, @"C:\\Program Files\\New Relic\\.NET Agent\\newcore\\NewRelic.Api.Agent.dll");

            // Create folders, files in the 'Logs' and 'Extensions' folders
            FileOperations.CreateFileOrDirectory(_tServer.MgmtScope, @"C:\ProgramData\New Relic\.NET Agent\Logs\LogsSubdirectory", true);
            FileOperations.CreateFileOrDirectory(_tServer.MgmtScope, @"C:\ProgramData\New Relic\.NET Agent\Extensions\ExtensionsSubdirectory", true);
            FileOperations.CreateFileOrDirectory(_tServer.MgmtScope, @"C:\ProgramData\New Relic\.NET Agent\Logs\Logs.txt");
            FileOperations.CreateFileOrDirectory(_tServer.MgmtScope, @"C:\ProgramData\New Relic\.NET Agent\Logs\LogsSubdirectory\LogsSubdirectory.txt");
            FileOperations.CreateFileOrDirectory(_tServer.MgmtScope, @"C:\ProgramData\New Relic\.NET Agent\Extensions\Extensions.txt");
            FileOperations.CreateFileOrDirectory(_tServer.MgmtScope, @"C:\ProgramData\New Relic\.NET Agent\Extensions\ExtensionsSubdirectory\ExtensionsSubdirectory.txt");

            // Delete the 'COR_ENABLE_PROFILING' and 'COR_PROFILER' environment variables
            _tServer.DeleteEnvironmentVariable("COR_ENABLE_PROFILING");
            _tServer.DeleteEnvironmentVariable("COR_PROFILER");
            _tServer.DeleteEnvironmentVariable("NEWRELIC_INSTALL_PATH");
            _tServer.DeleteEnvironmentVariable("CORECLR_PROFILER");
            _tServer.DeleteEnvironmentVariable("CORECLR_NEWRELIC_HOME");

            // Repair and verify the API dll was restored
            _tServer.CommandLineRepair(testName: nameof(RepairRestoresAPIAndAllAppsLeavesExtensionsLogsConfig));
            Assert.IsTrue(FileOperations.FileOrDirectoryExists(_tServer.MgmtScope, @"C:\\Program Files\\New Relic\\.NET Agent\\netframework\\NewRelic.Api.Agent.dll"), @"C:\\Program Files\\New Relic\\.NET Agent\\netframework\\NewRelic.Api.Agent.dll");
            Assert.IsTrue(FileOperations.FileOrDirectoryExists(_tServer.MgmtScope, @"C:\\Program Files\\New Relic\\.NET Agent\\netcore\\NewRelic.Api.Agent.dll"), @"C:\\Program Files\\New Relic\\.NET Agent\\netcore\\NewRelic.Api.Agent.dll");

            // Verify the environment variables were restored
            Assert.AreEqual("1", _tServer.FetchEnvironmentVariableValue("COR_ENABLE_PROFILING"), "COR_ENABLE_PROFILING");
            Assert.AreEqual("{71DA0A04-7777-4EC6-9643-7D28B46A8A41}", _tServer.FetchEnvironmentVariableValue("COR_PROFILER"), "COR_PROFILER");
            Assert.AreEqual(@"C:\Program Files\New Relic\.NET Agent\", _tServer.FetchEnvironmentVariableValue("NEWRELIC_INSTALL_PATH"), "NEWRELIC_INSTALL_PATH");
            Assert.AreEqual("{36032161-FFC0-4B61-B559-F6C5D41BAE5A}", _tServer.FetchEnvironmentVariableValue("CORECLR_PROFILER"), "CORECLR_PROFILER");
            Assert.AreEqual(@"C:\ProgramData\New Relic\.NET Agent\", _tServer.FetchEnvironmentVariableValue("CORECLR_NEWRELIC_HOME"), "CORECLR_NEWRELIC_HOME");

            // Verify the 'Logs' and 'Extensions' subfolders and files exist
            Assert.IsTrue(FileOperations.FileOrDirectoryExists(_tServer.MgmtScope, @"C:\\ProgramData\\New Relic\\.NET Agent\\Logs\\LogsSubdirectory\\LogsSubdirectory.txt"), @"C:\\ProgramData\\New Relic\\.NET Agent\\Logs\\LogsSubdirectory\\LogsSubdirectory.txt");
            Assert.IsTrue(FileOperations.FileOrDirectoryExists(_tServer.MgmtScope, @"C:\\ProgramData\\New Relic\\.NET Agent\\Logs\\Logs.txt"), @"C:\\ProgramData\\New Relic\\.NET Agent\\Logs\\Logs.txt");
            Assert.IsTrue(FileOperations.FileOrDirectoryExists(_tServer.MgmtScope, @"C:\\ProgramData\\New Relic\\.NET Agent\\Extensions\\ExtensionsSubdirectory\\ExtensionsSubdirectory.txt"), @"C:\\ProgramData\\New Relic\\.NET Agent\\Extensions\\ExtensionsSubdirectory\\ExtensionsSubdirectory.txt");
            Assert.IsTrue(FileOperations.FileOrDirectoryExists(_tServer.MgmtScope, @"C:\\ProgramData\\New Relic\\.NET Agent\\Extensions\\Extensions.txt"), @"C:\\ProgramData\\New Relic\\.NET Agent\\Extensions\\Extensions.txt");

            // Verify values in the 'newrelic.config' file
            var config = _tServer.LoadXmlFile(String.Format("{0}\\newrelic.config", _tServer.DataPath));
            var nsmgr = new XmlNamespaceManager(config.NameTable);
            nsmgr.AddNamespace("ns", "urn:newrelic-config");

            // Verify the license was correctly set by the installer
            Assert.AreEqual(Settings.LicenseKey, config.SelectSingleNode("//ns:service/@licenseKey", nsmgr).Value, "licenseKey");
            Assert.AreEqual("staging-collector.newrelic.com", config.SelectSingleNode("//ns:service/@host", nsmgr).Value, "host");
            Assert.AreEqual("true", config.SelectSingleNode("//ns:log/@auditLog", nsmgr).Value, "auditLog");
        }

        [Test]
        [Description("Verifies the 'newrelic.config' values are copied from the old installer to the new. Covers Core and Framework.")]
        public void OldToNewUpgrade()
        {
            _tServer.CommandLineInstallOldInstall();
            // Verify the configs before the upgrade
            var oldConfig = _tServer.LoadXmlFile(String.Format("{0}\\newrelic.xml", _tServer.DataPath));
            var oldNsmgr = new XmlNamespaceManager(oldConfig.NameTable);
            oldNsmgr.AddNamespace("ns", "urn:newrelic-config");

            // Verify the license was correctly set by the installer
            Assert.AreEqual(Settings.LicenseKey, oldConfig.SelectSingleNode("//ns:service/@licenseKey", oldNsmgr).Value);

            _tServer.CommandLineInstall(null, testName: nameof(OldToNewUpgrade));
            ComponentManager.TruncateComponents(_tServer);

            // Verify the configs after the upgrade
            var config = _tServer.LoadXmlFile(String.Format("{0}\\newrelic.config", _tServer.DataPath));
            var nsmgr = new XmlNamespaceManager(config.NameTable);
            nsmgr.AddNamespace("ns", "urn:newrelic-config");

            // Verify the license was correctly set by the installer
            Assert.AreEqual(Settings.LicenseKey, config.SelectSingleNode("//ns:service/@licenseKey", nsmgr).Value);
            Assert.AreEqual("staging-collector.newrelic.com", config.SelectSingleNode("//ns:service/@host", nsmgr).Value);
            Assert.AreEqual("true", config.SelectSingleNode("//ns:log/@auditLog", nsmgr).Value);

            // Verify the 'Logs' and 'Extensions' subfolders and files exist
            Assert.IsTrue(FileOperations.FileOrDirectoryExists(_tServer.MgmtScope, @"C:\\ProgramData\\New Relic\\.NET Agent\\Logs\\Log.txt"));
            Assert.IsTrue(FileOperations.FileOrDirectoryExists(_tServer.MgmtScope, @"C:\\ProgramData\\New Relic\\.NET Agent\\Extensions\\ExtensionsSubdirectory\\ExtensionsSubdirectory.txt"));
            Assert.IsTrue(FileOperations.FileOrDirectoryExists(_tServer.MgmtScope, @"C:\\ProgramData\\New Relic\\.NET Agent\\Extensions\\Extensions.txt"));
        }

        [Test]
        [Description("Verifies that the installer only removes new relic values from WAS and W3SVC on uninstall. Covers Core and Framework.")]
        public void UninstallDoesNotRemoveUserCreatedValues()
        {
            String[] expectedValues = new String[] { "EXISTING_DATA=1" };

            // Create name/value pairs in the W3SVC and WAS keys
            _tServer.CreateOrUpdateRegistryKey(RegistryHive.LocalMachine, @"SYSTEM\CurrentControlSet\Services\W3SVC", @"Environment", new string[] { "EXISTING_DATA=1" });
            _tServer.CreateOrUpdateRegistryKey(RegistryHive.LocalMachine, @"SYSTEM\CurrentControlSet\Services\WAS", @"Environment", new string[] { "EXISTING_DATA=1" });

            _tServer.CommandLineInstall(Settings.LicenseKey, testName: nameof(UninstallDoesNotRemoveUserCreatedValues));
            ComponentManager.TruncateComponents(_tServer);
            _tServer.CommandLineUninstall(testName: nameof(UninstallDoesNotRemoveUserCreatedValues));

            Assert.AreEqual(expectedValues, _tServer.GetRegistryKeyValue(RegistryHive.LocalMachine, @"SYSTEM\CurrentControlSet\Services\W3SVC", "Environment"));
            Assert.AreEqual(expectedValues, _tServer.GetRegistryKeyValue(RegistryHive.LocalMachine, @"SYSTEM\CurrentControlSet\Services\WAS", "Environment"));
            ComponentManager.CleanComponents(_tServer, testName: nameof(UninstallDoesNotRemoveUserCreatedValues));
        }

        [Test]
        [Description("Verifies the 'Logs' folder is re-created by the agent when removed.")]
        public void LogsFolderReCreatedWhenDeleted()
        {
            var installTestApp = new TestApplication(application: Applications.DotNet_Functional_InstallTestApp);
            installTestApp.TServer.CommandLineInstall(licenseKey: Settings.LicenseKey, testName: nameof(LogsFolderReCreatedWhenDeleted));
            ComponentManager.TruncateComponents(_tServer);

            installTestApp.TServer.ModifyOrCreateXmlAttribute("//x:configuration", "agentEnabled", "true");
            installTestApp.TServer.ModInstallAppWebConfigXML(true);

            // Start the service, let run, stop the service
            installTestApp.TServer.IISCommand("Start");
            installTestApp.SimpleTestRequest(resource: "service/start");
            installTestApp.WaitForLog(TestApplication.LogEntry.fullyConnected);
            installTestApp.TServer.IISCommand("Stop");

            Assert.IsTrue(FileOperations.FileOrDirectoryExists(_tServer.MgmtScope, @"C:\\ProgramData\\New Relic\\.NET Agent\\Logs", true), "Logs folder was not recreated.");
        }

        [Test]
        [Description("Verifies the usage of the 'NEW_RELIC_HOST' environment variable.")]
        public void EnvironmentVariable__NEW_RELIC_HOST()
        {
            var installTestApp = new TestApplication(application: Applications.DotNet_Functional_InstallTestApp);
            installTestApp.TServer.CreateEnvironmentVariable("NEW_RELIC_HOST", "staging-collector.newrelic.com");
            installTestApp.TServer.CommandLineInstall(licenseKey: Settings.LicenseKey, setCollectorHost: false, testName: nameof(EnvironmentVariable__NEW_RELIC_HOST));
            ComponentManager.TruncateComponents(_tServer);

            installTestApp.TServer.ModInstallAppWebConfigXML(true);
            installTestApp.TServer.IISCommand("Start");
            installTestApp.SimpleTestRequest(resource: "service/start");
            installTestApp.WaitForLog(TestApplication.LogEntry.fullyConnected);
            installTestApp.TServer.IISCommand("Stop");

            var expectedLogMessageConnected = String.Format(" Reporting to: https://staging.newrelic.com/accounts/{0}/applications/", installTestApp.AccountId);
            Assert.IsTrue(installTestApp.AgentLog.Contains(expectedLogMessageConnected), "Application is not connected.");
            Assert.IsTrue(Regex.IsMatch(installTestApp.AgentLog, " Received : {\"return_value\":{\"redirect_host\":\"staging-collector-[0-9]+\\.newrelic\\.com\"}}"), "Did not receive a successful response from the collector proxy.");
        }

        [Test]
        [Description("Verifies the usage of the 'NEWRELIC_LICENSEKEY' environment variable.")]
        public void EnvironmentVariable__NEWRELIC_LICENSEKEY()
        {
            var installTestApp = new TestApplication(application: Applications.DotNet_Functional_InstallTestApp);
            installTestApp.TServer.CreateEnvironmentVariable("NEWRELIC_LICENSEKEY", Settings.LicenseKey);
            installTestApp.TServer.CommandLineInstall(licenseKey: null, allFeatures: true, testName: nameof(EnvironmentVariable__NEWRELIC_LICENSEKEY));
            ComponentManager.TruncateComponents(_tServer);

            installTestApp.TServer.ModInstallAppWebConfigXML(true);
            installTestApp.TServer.IISCommand("Start");
            installTestApp.SimpleTestRequest(resource: "service/start");
            installTestApp.WaitForLog(TestApplication.LogEntry.fullyConnected);
            installTestApp.TServer.IISCommand("Stop");

            var expected = String.Format(" Reporting to: https://staging.newrelic.com/accounts/{0}/applications/", Common.StagingAccountId);
            Assert.IsTrue(installTestApp.AgentLog.Contains(expected), "Agent is not connected.");
        }
    }
}
