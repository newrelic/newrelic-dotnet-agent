using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Xml;
using FunctionalTests.Attributes;
using FunctionalTests.Helpers;
using Microsoft.Win32;
using NUnit.Framework;

namespace FunctionalTests
{
    [TestFixture]
    [Category("Install")]
    [Description("Verifies items created and/or set by the installer.")]
    public class Install : TestFixtureBaseAllOptions
    {
        private const string CorEnableProfile = "COR_ENABLE_PROFILING=1";
        private const string CorProfiler = "COR_PROFILER={71DA0A04-7777-4EC6-9643-7D28B46A8A41}";
        private const String NewRelicInstallPath = @"NEWRELIC_INSTALL_PATH=C:\Program Files\New Relic\.NET Agent\";
        private const string CoreCorProfiler = "CORECLR_PROFILER={36032161-FFC0-4B61-B559-F6C5D41BAE5A}";
        private const String CoreNewRelicHomePath = @"CORECLR_NEWRELIC_HOME=C:\ProgramData\New Relic\.NET Agent\";

        #region Program Files
        //Reference dotnet_agent\Build\ArtifactBuilder\FrameworkAgentComponents.cs for the canonical list of what should be laid down and where
        //Agent DLLs for the extensions dir
        [TestCase(@"netframework\\Extensions\\NewRelic.Core.dll")]
        [TestCase(@"netframework\\Extensions\\NewRelic.Parsing.dll")]
        [TestCase(@"netcore\\Extensions\\NewRelic.Core.dll")]
        [TestCase(@"netcore\\Extensions\\NewRelic.Parsing.dll")]
        //Storage providers
        [TestCase(@"netframework\\Extensions\\NewRelic.Providers.Storage.CallContext.dll")]
        [TestCase(@"netframework\\Extensions\\NewRelic.Providers.Storage.HttpContext.dll")]
        [TestCase(@"netframework\\Extensions\\NewRelic.Providers.Storage.OperationContext.dll")]
        [TestCase(@"netframework\\Extensions\\NewRelic.Providers.Storage.AsyncLocal.dll")]
        [TestCase(@"netcore\\Extensions\\NewRelic.Providers.Storage.AsyncLocal.dll")]
        //wrapper providers
        [TestCase(@"netframework\\Extensions\\NewRelic.Providers.Wrapper.Asp35.dll")]
        [TestCase(@"netframework\\Extensions\\NewRelic.Providers.Wrapper.AspNetCore.dll")]
        [TestCase(@"netframework\\Extensions\\NewRelic.Providers.Wrapper.CastleMonoRail2.dll")]
        [TestCase(@"netframework\\Extensions\\NewRelic.Providers.Wrapper.Couchbase.dll")]
        [TestCase(@"netframework\\Extensions\\NewRelic.Providers.Wrapper.HttpClient.dll")]
        [TestCase(@"netframework\\Extensions\\NewRelic.Providers.Wrapper.HttpWebRequest.dll")]
        [TestCase(@"netframework\\Extensions\\NewRelic.Providers.Wrapper.MongoDb.dll")]
        [TestCase(@"netframework\\Extensions\\NewRelic.Providers.Wrapper.MongoDb26.dll")]
        [TestCase(@"netframework\\Extensions\\NewRelic.Providers.Wrapper.Msmq.dll")]
        [TestCase(@"netframework\\Extensions\\NewRelic.Providers.Wrapper.Mvc3.dll")]
        [TestCase(@"netframework\\Extensions\\NewRelic.Providers.Wrapper.NServiceBus.dll")]
        [TestCase(@"netframework\\Extensions\\NewRelic.Providers.Wrapper.OpenRasta.dll")]
        [TestCase(@"netframework\\Extensions\\NewRelic.Providers.Wrapper.Owin.dll")]
        [TestCase(@"netframework\\Extensions\\NewRelic.Providers.Wrapper.RabbitMq.dll")]
        [TestCase(@"netframework\\Extensions\\NewRelic.Providers.Wrapper.RestSharp.dll")]
        [TestCase(@"netframework\\Extensions\\NewRelic.Providers.Wrapper.ScriptHandlerFactory.dll")]
        [TestCase(@"netframework\\Extensions\\NewRelic.Providers.Wrapper.ServiceStackRedis.dll")]
        [TestCase(@"netframework\\Extensions\\NewRelic.Providers.Wrapper.Sql.dll")]
        [TestCase(@"netframework\\Extensions\\NewRelic.Providers.Wrapper.StackExchangeRedis.dll")]
        [TestCase(@"netframework\\Extensions\\NewRelic.Providers.Wrapper.Wcf3.dll")]
        [TestCase(@"netframework\\Extensions\\NewRelic.Providers.Wrapper.WebApi1.dll")]
        [TestCase(@"netframework\\Extensions\\NewRelic.Providers.Wrapper.WebApi2.dll")]
        [TestCase(@"netframework\\Extensions\\NewRelic.Providers.Wrapper.WebOptimization.dll")]
        [TestCase(@"netframework\\Extensions\\NewRelic.Providers.Wrapper.WebServices.dll")]
        [TestCase(@"netcore\\Extensions\\NewRelic.Providers.Wrapper.AspNetCore.dll")]
        [TestCase(@"netcore\\Extensions\\NewRelic.Providers.Wrapper.HttpClient.dll")]
        [TestCase(@"netcore\\Extensions\\NewRelic.Providers.Wrapper.MongoDb26.dll")]
        [TestCase(@"netcore\\Extensions\\NewRelic.Providers.Wrapper.Sql.dll")]
        [TestCase(@"netcore\\Extensions\\NewRelic.Providers.Wrapper.StackExchangeRedis.dll")]
        // Home dir files
        [TestCase("default_newrelic.config")]
        [TestCase("License.txt")]
        [TestCase("THIRD_PARTY_NOTICES.txt")]
        [TestCase(@"netframework\\NewRelic.Agent.Core.dll")]
        [TestCase(@"netframework\\NewRelic.Agent.Extensions.dll")]
        [TestCase(@"netframework\\NewRelic.Api.Agent.dll")]
        [TestCase(@"netframework\\NewRelic.Profiler.dll")]
        [TestCase(@"netcore\\NewRelic.Agent.Core.dll")]
        [TestCase(@"netcore\\NewRelic.Agent.Extensions.dll")]
        [TestCase(@"netcore\\NewRelic.Api.Agent.dll")]
        [TestCase(@"netcore\\NewRelic.Profiler.dll")]
        [TestCase(@"NewRelic.Api.Agent.dll")]
        public void ProgramFiles(string filename)
        {
            var path = string.Format(@"C:\\Program Files\\New Relic\\.NET Agent\\{0}", filename);
            Assert.IsTrue(FileOperations.FileOrDirectoryExists(TServer.MgmtScope, path), "{0} was not found.", path);
        }

        [TestCase("flush_dotnet_temp.cmd")]
        public void ProgramFiles__Tools(string filename)
        {
            var path = string.Format(@"C:\\Program Files\\New Relic\\.NET Agent\\netframework\\Tools\\{0}", filename);
            Assert.IsTrue(FileOperations.FileOrDirectoryExists(TServer.MgmtScope, path), "{0} was not found.", filename);
        }

        [Test]
        [Description("Verifies the copyright date in the 'LICENSE.txt' file is correct.")]
        public void License_txt__Copyright_Date()
        {
            Assert.IsTrue(FileOperations.ParseTextFile(string.Format("{0}LICENSE.txt", TServer.InstallPath)).Contains(string.Format("Copyright (c) 2008-{0} New Relic, Inc.  All rights reserved.", DateTime.Now.Year)));
        }

        [Test]
        [Description("Verifies the 'default_newrelic.config' file structure is correct and has not been altered.")]
        public void default_newrelic_config_Correct()
        {
            var expected = new XmlDocument();
            expected.Load(string.Format(@"{0}\newrelic.config", TestContext.CurrentContext.TestDirectory));
            var actual = TServer.LoadXmlFile(string.Format("{0}default_newrelic.config", TServer.InstallPath));

            Assert.AreEqual(expected.InnerXml, actual.InnerXml);
        }
        #endregion Program Files

        #region ProgramData
        [TestCase(@"Extensions\\extension.xsd", false)]
        [TestCase(@"Extensions\\netframework\\NewRelic.Providers.Wrapper.Asp35.Instrumentation.xml", false)]
        [TestCase(@"Extensions\\netframework\\NewRelic.Providers.Wrapper.AspNetCore.Instrumentation.xml", false)]
        [TestCase(@"Extensions\\netframework\\NewRelic.Providers.Wrapper.CastleMonoRail2.Instrumentation.xml", false)]
        [TestCase(@"Extensions\\netframework\\NewRelic.Providers.Wrapper.Couchbase.Instrumentation.xml", false)]
        [TestCase(@"Extensions\\netframework\\NewRelic.Providers.Wrapper.HttpClient.Instrumentation.xml", false)]
        [TestCase(@"Extensions\\netframework\\NewRelic.Providers.Wrapper.HttpWebRequest.Instrumentation.xml", false)]
        [TestCase(@"Extensions\\netframework\\NewRelic.Providers.Wrapper.MongoDb.Instrumentation.xml", false)]
        [TestCase(@"Extensions\\netframework\\NewRelic.Providers.Wrapper.MongoDb26.Instrumentation.xml", false)]
        [TestCase(@"Extensions\\netframework\\NewRelic.Providers.Wrapper.Msmq.Instrumentation.xml", false)]
        [TestCase(@"Extensions\\netframework\\NewRelic.Providers.Wrapper.Mvc3.Instrumentation.xml", false)]
        [TestCase(@"Extensions\\netframework\\NewRelic.Providers.Wrapper.NServiceBus.Instrumentation.xml", false)]
        [TestCase(@"Extensions\\netframework\\NewRelic.Providers.Wrapper.OpenRasta.Instrumentation.xml", false)]
        [TestCase(@"Extensions\\netframework\\NewRelic.Providers.Wrapper.Owin.Instrumentation.xml", false)]
        [TestCase(@"Extensions\\netframework\\NewRelic.Providers.Wrapper.RabbitMq.Instrumentation.xml", false)]
        [TestCase(@"Extensions\\netframework\\NewRelic.Providers.Wrapper.RestSharp.Instrumentation.xml", false)]
        [TestCase(@"Extensions\\netframework\\NewRelic.Providers.Wrapper.ScriptHandlerFactory.Instrumentation.xml", false)]
        [TestCase(@"Extensions\\netframework\\NewRelic.Providers.Wrapper.ServiceStackRedis.Instrumentation.xml", false)]
        [TestCase(@"Extensions\\netframework\\NewRelic.Providers.Wrapper.Sql.Instrumentation.xml", false)]
        [TestCase(@"Extensions\\netframework\\NewRelic.Providers.Wrapper.StackExchangeRedis.Instrumentation.xml", false)]
        [TestCase(@"Extensions\\netframework\\NewRelic.Providers.Wrapper.Wcf3.Instrumentation.xml", false)]
        [TestCase(@"Extensions\\netframework\\NewRelic.Providers.Wrapper.WebApi1.Instrumentation.xml", false)]
        [TestCase(@"Extensions\\netframework\\NewRelic.Providers.Wrapper.WebApi2.Instrumentation.xml", false)]
        [TestCase(@"Extensions\\netframework\\NewRelic.Providers.Wrapper.WebOptimization.Instrumentation.xml", false)]
        [TestCase(@"Extensions\\netframework\\NewRelic.Providers.Wrapper.WebServices.Instrumentation.xml", false)]
        [TestCase(@"Extensions\\netframework\\NewRelic.Providers.Wrapper.Misc.Instrumentation.xml", false)]
        [TestCase(@"Extensions\\netcore\\NewRelic.Providers.Wrapper.AspNetCore.Instrumentation.xml", false)]
        [TestCase(@"Extensions\\netcore\\NewRelic.Providers.Wrapper.HttpClient.Instrumentation.xml", false)]
        [TestCase(@"Extensions\\netcore\\NewRelic.Providers.Wrapper.MongoDb26.Instrumentation.xml", false)]
        [TestCase(@"Extensions\\netcore\\NewRelic.Providers.Wrapper.Sql.Instrumentation.xml", false)]
        [TestCase(@"Extensions\\netcore\\NewRelic.Providers.Wrapper.StackExchangeRedis.Instrumentation.xml", false)]
        [TestCase(@"Extensions\\netcore\\NewRelic.Providers.Wrapper.Misc.Instrumentation.xml", false)]
        [TestCase("Logs", true)]
        [TestCase("newrelic.config", false)]
        [TestCase("newrelic.xsd", false)]
        public void ProgramData(string filename, bool directory)
        {
            var path = string.Format(@"C:\\ProgramData\\New Relic\\.NET Agent\\{0}", filename);
            Assert.IsTrue(FileOperations.FileOrDirectoryExists(TServer.MgmtScope, path, directory), "{0} was not found.", path);
        }

        #endregion ProgramData

        #region Registry
        [Test]
        [Description("Verifies the W3SVC Environment registry key is correctly set.")]
        public void W3SVC_Environment()
        {
            var registryValues = new List<string>(TServer.GetRegistryKeyValue(RegistryHive.LocalMachine, @"SYSTEM\CurrentControlSet\Services\W3SVC", "Environment") as string[]);
            Assert.NotNull(registryValues);
            Assert.IsTrue(registryValues.Contains(CorEnableProfile), CorEnableProfile);
            Assert.IsTrue(registryValues.Contains(CorProfiler), CorProfiler);
            Assert.IsTrue(registryValues.Contains(NewRelicInstallPath), NewRelicInstallPath);
            Assert.IsTrue(registryValues.Contains(CoreCorProfiler), CoreCorProfiler);
            Assert.IsTrue(registryValues.Contains(CoreNewRelicHomePath), CoreNewRelicHomePath);
        }

        [Test]
        public void WAS_Environment()
        {
            var registryValues = new List<string>(TServer.GetRegistryKeyValue(RegistryHive.LocalMachine, @"SYSTEM\CurrentControlSet\Services\WAS", "Environment") as string[]);
            Assert.NotNull(registryValues);
            Assert.IsTrue(registryValues.Contains(CorEnableProfile), CorEnableProfile);
            Assert.IsTrue(registryValues.Contains(CorProfiler), CorProfiler);
            Assert.IsTrue(registryValues.Contains(NewRelicInstallPath), NewRelicInstallPath);
            Assert.IsTrue(registryValues.Contains(CoreCorProfiler), CoreCorProfiler);
            Assert.IsTrue(registryValues.Contains(CoreNewRelicHomePath), CoreNewRelicHomePath);
        }

        [Test]
        public void NewRelicHome()
        {
            Assert.AreEqual(@"C:\ProgramData\New Relic\.NET Agent\", TServer.GetRegistryKeyValue(RegistryHive.LocalMachine, @"SOFTWARE\New Relic\.NET Agent", "NewRelicHome"));
        }

        [Test, DoesNotRunOn32BitOS]
        public void NewRelicHome_Wow6432Node()
        {
            Assert.AreEqual(@"C:\ProgramData\New Relic\.NET Agent\", TServer.GetRegistryKeyValue(RegistryHive.LocalMachine, @"SOFTWARE\Wow6432Node\New Relic\.NET Agent", "NewRelicHome"));
        }

        [Test]
        public void REG_71DA0A04_7777_4EC6_9643_7D28B46A8A41()
        {
            Assert.AreEqual("New Relic .NET Profiler", TServer.GetRegistryKeyValue(RegistryHive.ClassesRoot, @"CLSID\{71DA0A04-7777-4EC6-9643-7D28B46A8A41}", null));
        }

        [Test]
        public void REG_36032161_FFC0_4B61_B559_F6C5D41BAE5A()
        {
            Assert.AreEqual("New Relic .NET Profiler", TServer.GetRegistryKeyValue(RegistryHive.ClassesRoot, @"CLSID\{36032161-FFC0-4B61-B559-F6C5D41BAE5A}", null));
        }

        [Test, DoesNotRunOn32BitOS]
        public void REG_71DA0A04_7777_4EC6_9643_7D28B46A8A41_Wow6432Node()
        {
            Assert.AreEqual("New Relic .NET Profiler", TServer.GetRegistryKeyValue(RegistryHive.ClassesRoot, @"CLSID\Wow6432Node\CLSID\{71DA0A04-7777-4EC6-9643-7D28B46A8A41}", null));
        }

        [Test, DoesNotRunOn32BitOS]
        public void REG_36032161_FFC0_4B61_B559_F6C5D41BAE5A_Wow6432Node()
        {
            Assert.AreEqual("New Relic .NET Profiler", TServer.GetRegistryKeyValue(RegistryHive.ClassesRoot, @"CLSID\Wow6432Node\CLSID\{36032161-FFC0-4B61-B559-F6C5D41BAE5A}", null));
        }

        [Test]
        public void REG_71DA0A04_7777_4EC6_9643_7D28B46A8A41__InprocServer32()
        {
            Assert.AreEqual(@"C:\Program Files\New Relic\.NET Agent\netframework\NewRelic.Profiler.dll", TServer.GetRegistryKeyValue(RegistryHive.ClassesRoot, @"CLSID\{71DA0A04-7777-4EC6-9643-7D28B46A8A41}\InprocServer32", null));
        }

        [Test]
        public void REG_36032161_FFC0_4B61_B559_F6C5D41BAE5A__InprocServer32()
        {
            Assert.AreEqual(@"C:\Program Files\New Relic\.NET Agent\netcore\NewRelic.Profiler.dll", TServer.GetRegistryKeyValue(RegistryHive.ClassesRoot, @"CLSID\{36032161-FFC0-4B61-B559-F6C5D41BAE5A}\InprocServer32", null));
        }

        [Test, DoesNotRunOn32BitOS]
        public void REG_71DA0A04_7777_4EC6_9643_7D28B46A8A41__InprocServer32_Wow6432Node()
        {
            Assert.AreEqual(@"C:\Program Files (x86)\New Relic\.NET Agent\NewRelic.Profiler.dll", TServer.GetRegistryKeyValue(RegistryHive.ClassesRoot, @"CLSID\Wow6432Node\CLSID\{71DA0A04-7777-4EC6-9643-7D28B46A8A41}\InprocServer32", null));
        }

        [Test, DoesNotRunOn32BitOS]
        public void REG_36032161_FFC0_4B61_B559_F6C5D41BAE5A__InprocServer32_Wow6432Node()
        {
            Assert.AreEqual(@"C:\Program Files (x86)\New Relic\.NET Agent\NewRelic.Profiler.dll", TServer.GetRegistryKeyValue(RegistryHive.ClassesRoot, @"CLSID\Wow6432Node\CLSID\{36032161-FFC0-4B61-B559-F6C5D41BAE5A}\InprocServer32", null));
        }

        [Test]
        public void REG_71DA0A04_7777_4EC6_9643_7D28B46A8A41__Version()
        {
            Assert.AreEqual(Settings.AgentVersion, TServer.GetRegistryKeyValue(RegistryHive.ClassesRoot, @"CLSID\{71DA0A04-7777-4EC6-9643-7D28B46A8A41}\Version", null));
        }

        [Test]
        public void REG_36032161_FFC0_4B61_B559_F6C5D41BAE5A__Version()
        {
            Assert.AreEqual(Settings.AgentVersion, TServer.GetRegistryKeyValue(RegistryHive.ClassesRoot, @"CLSID\{36032161-FFC0-4B61-B559-F6C5D41BAE5A}\Version", null));
        }

        [Test, DoesNotRunOn32BitOS]
        public void REG_71DA0A04_7777_4EC6_9643_7D28B46A8A41__Version_Wow6432Node()
        {
            Assert.AreEqual(Settings.AgentVersion, TServer.GetRegistryKeyValue(RegistryHive.ClassesRoot, @"CLSID\Wow6432Node\CLSID\{71DA0A04-7777-4EC6-9643-7D28B46A8A41}\Version", null));
        }

        [Test, DoesNotRunOn32BitOS]
        public void REG_36032161_FFC0_4B61_B559_F6C5D41BAE5A__Version_Wow6432Node()
        {
            Assert.AreEqual(Settings.AgentVersion, TServer.GetRegistryKeyValue(RegistryHive.ClassesRoot, @"CLSID\Wow6432Node\CLSID\{36032161-FFC0-4B61-B559-F6C5D41BAE5A}\Version", null));
        }

        #endregion Registry

        [TestCase(@"C:\Program Files\New Relic\.NET Agent\netframework\NewRelic.Api.Agent.dll")]
        [TestCase(@"C:\Program Files\New Relic\.NET Agent\netframework\NewRelic.Agent.Core.dll")]
        [TestCase(@"C:\Program Files\New Relic\.NET Agent\netcore\NewRelic.Api.Agent.dll")]
        [TestCase(@"C:\Program Files\New Relic\.NET Agent\netcore\NewRelic.Agent.Core.dll")]
        [Description("Verifies the referenced files are strongly named.")]
        public void StronglyNamedAssemblies(string path)
        {
            var expected = string.Format(@"Assembly '{0}' is valid", path);
            Assert.IsTrue(ExecuteIsStronglyNamed(path).Contains(expected), @"The '{0}' does not appear to be signed!!", path);
        }

        [TestCase(@"Program Files\New Relic\.NET Agent\netframework\Extensions")]
        [TestCase(@"Program Files\New Relic\.NET Agent\netcore\Extensions")]
        public void StronglyNamedAssemblies_Extensions(string pathSuffix)
        {
            var path = string.Format(@"{0}{1}", TServer.DriveRoot, pathSuffix);
            var assemblies = Directory.EnumerateFiles(path, "NewRelic.*.dll", SearchOption.TopDirectoryOnly);
            foreach (var assemblyPath in assemblies)
            {
                var assembly = assemblyPath;
                Console.WriteLine("Checking for strongly named assembly: {0}.", assembly);
                if (Settings.Environment != Enumerations.EnvironmentSetting.Local)
                {
                    assembly = assemblyPath.Replace(string.Format(@"\\{0}\C$\", Settings.RemoteServers[0]), @"C:\");
                }

                var expected = string.Format(@"Assembly '{0}' is valid", assembly);
                Assert.IsTrue(ExecuteIsStronglyNamed(assembly).Contains(expected), "{0} is not strongly named.", assembly);
            }
        }

        #region Profiler Loading/Unloading
        [Test]
        [Description("Verifies the 'NewRelic.Profiler.dll' is loaded when the 'NewRelic.AgentEnabled' flag is set to 'true'.")]
        public void AgentEnabledTrue_ILLoaded()
        {
            var installTestApp = new TestApplication(application: Applications.DotNet_Functional_InstallTestApp);
            TServer.IISCommand("Stop");
            TServer.PurgeAgentLogs();

            TServer.ModifyOrCreateXmlAttribute("//x:configuration", "agentEnabled", "true");
            TServer.ModInstallAppWebConfigXML(agentEnabled: true);

            // Start the service, wait for logging to occur
            TServer.IISCommand("Start");
            installTestApp.SimpleTestRequest(resource: "service/start");
            installTestApp.WaitForLog(TestApplication.LogEntry.fullyConnected);

            // Verify the 'NewRelic.Profiler' is loaded, stop the service
            Assert.IsTrue(Common.FileLoadedInProcess(TServer.MgmtScope, "NewRelic.Profiler", "w3wp.exe"));
            TServer.IISCommand("Stop");
        }

        [Test]
        [Description("Verifies the 'NewRelic.Core.dll' is not loaded when the 'NewRelic.AgentEnabled' flag is set to 'false' in the 'web.config'.")]
        public void AgentEnabledFalse_NewRelicCoreNotLoaded()
        {
            var installTestApp = new TestApplication(application: Applications.DotNet_Functional_InstallTestApp);
            TServer.IISCommand("Stop");
            TServer.PurgeAgentLogs();

            TServer.ModifyOrCreateXmlAttribute("//x:configuration", "agentEnabled", "true");
            TServer.ModInstallAppWebConfigXML(agentEnabled: false);

            // Start the service, let run for 5 seconds
            TServer.IISCommand("Start");
            installTestApp.SimpleTestRequest(resource: "service/start");
            Thread.Sleep(5000);

            // Verify the 'NewRelic.Core' is NOT loaded
            //Web apps load and keep the profiler, agent.core, and extensions in the scenario.  Console apps drop everything.
            Assert.IsFalse(Common.FileLoadedInProcess(TServer.MgmtScope, "NewRelic.Core", "w3wp.exe"));
            TServer.IISCommand("Stop");
        }

        [Test]
        [Description("Verifies the 'NewRelic.Profiler.dll' is not loaded when the 'NewRelic.AgentEnabled' flag is set to 'false' in the 'newrelic.config'.")]
        public void NewRelicAgentEnabledFalse_ProfilerNotLoaded()
        {
            var installTestApp = new TestApplication(application: Applications.DotNet_Functional_InstallTestApp);
            TServer.IISCommand("Stop");
            TServer.PurgeAgentLogs();

            TServer.ModifyOrCreateXmlAttribute("//x:configuration", "agentEnabled", "false");
            TServer.ModInstallAppWebConfigXML(agentEnabled: true);

            // Start the service, let run for 5 seconds
            TServer.IISCommand("Start");
            installTestApp.SimpleTestRequest(resource: "service/start");
            Thread.Sleep(5000);

            // Verify the 'NewRelic.Profiler' is NOT loaded
            Assert.IsFalse(Common.FileLoadedInProcess(TServer.MgmtScope, "NewRelic.Profiler", "w3wp.exe"));
            TServer.IISCommand("Stop");
        }
        #endregion Profiler Loading/Unloading


        #region Private Methods
        /// <summary>
        /// Executes the strong name utility to determine if the specified file is signed.
        /// </summary>
        /// <param name="filePath">The path to the file to be checked.</param>
        /// <returns>Output from the batch script that checks if the .dll is signed.</returns>
        private string ExecuteIsStronglyNamed(string filePath)
        {
            var command = string.Format("cmd.exe /c sn.exe -vf \"{0}\" > C:\\cmdOutput.txt", filePath);
            WMI.MakeWMICall(TServer.MgmtScope, "Win32_Process", command, @"C:\Program Files\Microsoft SDKs\Windows\v7.1\Bin\NETFX 4.0 Tools\");

            var result = FileOperations.ParseTextFile(string.Format(@"{0}\cmdOutput.txt", TServer.DriveRoot));
            FileOperations.DeleteFileOrDirectory(TServer.MgmtScope, @"C:\\cmdOutput.txt");

            return result;
        }
        #endregion Private Methods
    }
}
