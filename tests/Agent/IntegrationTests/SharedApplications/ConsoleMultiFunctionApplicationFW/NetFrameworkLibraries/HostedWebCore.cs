// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using MultiFunctionApplicationHelpers;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTests.Shared.ReflectionHelpers;
using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Xml;

namespace ConsoleMultiFunctionApplicationFW.NetFrameworkLibraries
{
    [Library]
    public class HostedWebCore
    {
        private readonly string _executionFolder;

        private readonly string _hostedApplicationVirtualDirectory = Guid.NewGuid().ToString();
        private string _hostedApplicationRoot => _executionFolder;
        private string _applicationHostConfigFilePath => Path.Combine(HostedApplicationFolder, "applicationHost.config");
        private string _applicationHostConfigTemplatePath => Path.Combine(_executionFolder, "NetFrameworkLibraries", "applicationHost.config.template");


        public string HostedApplicationFolder => Path.Combine(_hostedApplicationRoot, _hostedApplicationVirtualDirectory);
        public string WebConfigPath => Path.Combine(HostedApplicationFolder, "Web.Config");


        public HostedWebCore()
        {
            var fileRef = new FileInfo(Assembly.GetEntryAssembly().Location);
            _executionFolder = fileRef.Directory.FullName;
        }


        /// <summary>
        /// Responsible for making a copy of the application to host and placing it into a temporary directory
        /// Also configures the applicationHost.config file to based on this app
        /// </summary>
        [LibraryMethod]
        public void StageWebApp(string sourcePath, int port)
        {
            var srcDir = new DirectoryInfo(sourcePath);

            ConsoleMFLogger.Info("Staging Compiled Web Application");
            ConsoleMFLogger.Info($"Source      > {srcDir.ToString()}");
            ConsoleMFLogger.Info($"Destination > {HostedApplicationFolder}");
            ConsoleMFLogger.Info();
            DirectoryCopy(sourcePath, HostedApplicationFolder);

            ConsoleMFLogger.Info("Configuring applicationHost.config");
            ConsoleMFLogger.Info($"Port         > {port}");
            ConsoleMFLogger.Info();
            ConfigureAppHostConfig(port);
        }


        /// <summary>
        /// After the app has been staged, starts the web server
        /// </summary>
        /// <param name="port"></param>
        /// <param name="processDescription"></param>
        [LibraryMethod]
        public void Start()
        {
            var instanceName = $"New Relic HWC Testing {_hostedApplicationVirtualDirectory}";

            ConsoleMFLogger.Info("Starting Hosted Web Core");
            ConsoleMFLogger.Info($"Instance     > {instanceName}");
            ConsoleMFLogger.Info();

            HWC.Activate(_applicationHostConfigFilePath, null, instanceName);
        }

        /// <summary>
        /// Stops the web server
        /// </summary>
        [LibraryMethod]
        public void Stop()
        {
            ConsoleMFLogger.Info("Stopping Hosted Web Core");
            HWC.Shutdown(true);
        }


        [LibraryMethod]
        public void Config_ASPNetCompatibility(bool isEnabled)
        {
            XmlUtils.ModifyOrCreateXmlAttribute(WebConfigPath, "", new[] { "configuration", "system.serviceModel", "serviceHostingEnvironment" }, "aspNetCompatibilityEnabled", "false");
        }

        /// <summary>
        /// Configures the application host file that is used to define the application and the app pool.  This is a lof of what the IISAdmin
        /// tool helps you manage.
        /// </summary>
        /// <param name="port"></param>
        private void ConfigureAppHostConfig(int port)
        {
            var l = new XmlDocument();
            l.Load(_applicationHostConfigTemplatePath);

            var w3cLogNode = l.SelectSingleNode("configuration/system.applicationHost/log/centralW3CLogFile");
            w3cLogNode.Attributes["directory"].Value = HostedApplicationFolder;

            var binaryLogNode = l.SelectSingleNode("configuration/system.applicationHost/log/centralBinaryLogFile");
            binaryLogNode.Attributes["directory"].Value = HostedApplicationFolder;


            var siteLogFile = l.SelectSingleNode("configuration/system.applicationHost/sites/siteDefaults/logFile");
            siteLogFile.Attributes["directory"].Value = HostedApplicationFolder;

            var siteFailedReqLogging = l.SelectSingleNode("configuration/system.applicationHost/sites/siteDefaults/traceFailedRequestsLogging");
            siteFailedReqLogging.Attributes["directory"].Value = HostedApplicationFolder;

            var virtualDirectoryNode = l.SelectSingleNode("configuration/system.applicationHost/sites/site/application/virtualDirectory");
            virtualDirectoryNode.Attributes["physicalPath"].Value = HostedApplicationFolder;

            var bindingNode = l.SelectSingleNode("configuration/system.applicationHost/sites/site/bindings/binding");
            bindingNode.Attributes["bindingInformation"].Value = $"*:{port}:";

            var appPoolName = $"HWCTest{port}-{Guid.NewGuid()}";

            var appPoolNode = l.SelectSingleNode("configuration/system.applicationHost/applicationPools/add");
            appPoolNode.Attributes["name"].Value = appPoolName;


            var appDefaultsNode = l.SelectSingleNode("configuration/system.applicationHost/sites/applicationDefaults");
            appDefaultsNode.Attributes["applicationPool"].Value = appPoolName;

            l.Save(_applicationHostConfigFilePath);
        }


        private static void DirectoryCopy(string sourceDirName, string destDirName)
        {
            // Get the subdirectories for the specified directory.
            DirectoryInfo dir = new DirectoryInfo(sourceDirName);

            if (!dir.Exists)
            {
                throw new DirectoryNotFoundException(
                    "Source directory does not exist or could not be found: "
                    + sourceDirName);
            }

            DirectoryInfo[] dirs = dir.GetDirectories();
            // If the destination directory doesn't exist, create it.
            if (!Directory.Exists(destDirName))
            {
                Directory.CreateDirectory(destDirName);
            }

            // Get the files in the directory and copy them to the new location.
            FileInfo[] files = dir.GetFiles();
            foreach (FileInfo file in files)
            {
                string temppath = Path.Combine(destDirName, file.Name);
                file.CopyTo(temppath, false);
            }

            // copy subdirectories, copy them and their contents to new location.
            foreach (DirectoryInfo subdir in dirs)
            {
                string temppath = Path.Combine(destDirName, subdir.Name);
                DirectoryCopy(subdir.FullName, temppath);
            }
        }
    }


    #region Hostable WebCore
    internal static class HWC
    {

        private static bool _isActivated;


        private delegate int FnWebCoreActivate([In, MarshalAs(UnmanagedType.LPWStr)]string appHostConfig, [In, MarshalAs(UnmanagedType.LPWStr)]string rootWebConfig, [In, MarshalAs(UnmanagedType.LPWStr)]string instanceName);
        private delegate int FnWebCoreShutdown(bool immediate);

        private static FnWebCoreActivate WebCoreActivate;
        private static FnWebCoreShutdown WebCoreShutdown;

        static HWC()
        {
            // Load the library and get the function pointers for the WebCore entry points
            const string HWCPath = @"%windir%\system32\inetsrv\hwebcore.dll";
            IntPtr hwc = NativeMethods.LoadLibrary(Environment.ExpandEnvironmentVariables(HWCPath));

            IntPtr procaddr = NativeMethods.GetProcAddress(hwc, "WebCoreActivate");
            WebCoreActivate = (FnWebCoreActivate)Marshal.GetDelegateForFunctionPointer(procaddr, typeof(FnWebCoreActivate));

            procaddr = NativeMethods.GetProcAddress(hwc, "WebCoreShutdown");
            WebCoreShutdown = (FnWebCoreShutdown)Marshal.GetDelegateForFunctionPointer(procaddr, typeof(FnWebCoreShutdown));
        }

        /// <summary>
        /// Specifies if Hostable WebCore ha been activated
        /// </summary>
        public static bool IsActivated
        {
            get
            {
                return _isActivated;
            }
        }

        /// <summary>
        /// Activate the HWC
        /// </summary>
        /// <param name="appHostConfig">Path to ApplicationHost.config to use</param>
        /// <param name="rootWebConfig">Path to the Root Web.config to use</param>
        /// <param name="instanceName">Name for this instance</param>
        public static void Activate(string appHostConfig, string rootWebConfig, string instanceName)
        {
            int result = WebCoreActivate(appHostConfig, rootWebConfig, instanceName);
            if (result != 0)
            {
                Console.WriteLine($"{result}    ${result:X}");

                Marshal.ThrowExceptionForHR(result);
            }

            _isActivated = true;
        }

        /// <summary>
        /// Shutdown HWC
        /// </summary>
        public static void Shutdown(bool immediate)
        {
            if (_isActivated)
            {
                WebCoreShutdown(immediate);
                _isActivated = false;
            }
        }


        private static class NativeMethods
        {
            [DllImport("kernel32.dll")]
            internal static extern IntPtr LoadLibrary(string dllname);

            [DllImport("kernel32.dll")]
            internal static extern IntPtr GetProcAddress(IntPtr hModule, string procname);
        }
    }
    #endregion


}
