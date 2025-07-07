// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Xunit;

namespace NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;

public class RemoteWebApplication : RemoteApplication
{
    private readonly string _applicationDirectoryName;

    private const string HostedWebCoreProcessName = @"HostedWebCore.exe";

    protected override string SourceApplicationDirectoryPath { get { return Path.Combine(SourceApplicationsDirectoryPath, ApplicationDirectoryName, "Deploy"); } }

    private static readonly string SourceHostedWebCoreProjectDirectoryPath = Path.Combine(SourceIntegrationTestsSolutionDirectoryPath, "HostedWebCore");

    private static readonly string SourceHostedWebCoreDirectoryPath = Path.Combine(SourceHostedWebCoreProjectDirectoryPath, "bin", Utilities.Configuration, HostedWebCoreTargetFramework);

    protected override string ApplicationDirectoryName { get { return _applicationDirectoryName; } }

    private string DestinationHostedWebCoreDirectoryPath { get { return Path.Combine(DestinationRootDirectoryPath, "HostedWebCore"); } }

    private string DestinationHostedWebCoreExecutablePath { get { return Path.Combine(DestinationHostedWebCoreDirectoryPath, HostedWebCoreProcessName); } }

    private string DestinationApplicationHostConfigFilePath { get { return Path.Combine(DestinationHostedWebCoreDirectoryPath, "applicationHost.config"); } }

    private string DestinationApplicationWebConfigFilePath { get { return Path.Combine(DestinationApplicationDirectoryPath, "Web.config"); } }

    public RemoteWebApplication(string applicationDirectoryName, ApplicationType applicationType) : base(applicationType)
    {
        _applicationDirectoryName = applicationDirectoryName;

        ValidateHostedWebCoreOutput = true;
    }

    public override void CopyToRemote()
    {
        CopyNewRelicHomeDirectoryToRemote();
        CopyApplicationDirectoryToRemote();
        CopyHostedWebCoreToRemote();
        SetNewRelicAppNameInWebConfig();
        SetUpApplicationHostConfig();
        ModifyNewRelicConfig();

        CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(DestinationNewRelicConfigFilePath, new[] { "configuration", "instrumentation", "applications", "application" }, "name", HostedWebCoreProcessName);
    }

    protected override string GetStartInfoArgs(string arguments)
    {
        return $"--port={Port} {arguments}";
    }
    protected override string StartInfoFileName => Path.Combine(DestinationHostedWebCoreDirectoryPath, "HostedWebCore.exe");

    protected override string StartInfoWorkingDirectory => DestinationHostedWebCoreDirectoryPath;

    protected override void WaitForProcessToStartListening(bool captureStandardOutput)
    {
        var pidFilePath = DestinationHostedWebCoreExecutablePath + ".pid";
        Console.Write("[" + DateTime.Now + "] Waiting for process to start (" + pidFilePath + ") ... " + Environment.NewLine);
        var stopwatch = Stopwatch.StartNew();
        while (!RemoteProcess.HasExited && stopwatch.Elapsed < Timing.TimeToColdStart)
        {
            if (File.Exists(pidFilePath))
            {
                Console.Write("Done." + Environment.NewLine);
                return;
            }
            Thread.Sleep(Timing.TimeBetweenFileExistChecks);
        }

        if (!RemoteProcess.HasExited)
        {
            try
            {
                //We need to attempt to clean up the process that did not successfully start.
                RemoteProcess.Kill();
            }
            catch (Exception)
            {
                TestLogger?.WriteLine("[RemoteWebApplication]: WaitForHostedWebCoreToStartListening could not kill hung remote process.");
            }
        }

        if (captureStandardOutput)
        {
            CapturedOutput.WriteProcessOutputToLog("[RemoteWebApplication]: WaitForHostedWebCoreToStartListening");
        }

        Assert.Fail("Remote process never generated a .pid file!");
    }

    private void CopyHostedWebCoreToRemote()
    {
        Directory.CreateDirectory(DestinationHostedWebCoreDirectoryPath);
        CommonUtils.CopyDirectory(SourceHostedWebCoreDirectoryPath, DestinationHostedWebCoreDirectoryPath);
    }

    private void SetNewRelicAppNameInWebConfig()
    {
        var nodes = new[]
        {
            "configuration",
            "appSettings",
            "add",
        };
        var attributes = new[]
        {
            new KeyValuePair<string, string>("key", "NewRelic.AppName"),
            new KeyValuePair<string, string>("value", AppName),
        };
        XmlUtils.ModifyOrCreateXmlAttributes(DestinationApplicationWebConfigFilePath, string.Empty, nodes, attributes);
    }

    private void SetUpApplicationHostConfig()
    {
        SetSiteVirtualDirectoryInApplicationHostConfig();
        SetSitePortInApplicationHostConfig();
        SetApplicationPoolInApplicationHostConfig();
    }

    private void SetSiteVirtualDirectoryInApplicationHostConfig()
    {
        var path = DestinationApplicationDirectoryPath.StartsWith("\\")
            ? CommonUtils.GetLocalPathFromRemotePath(DestinationApplicationDirectoryPath)
            : DestinationApplicationDirectoryPath;

        var nodes = new[]
        {
            "configuration",
            "system.applicationHost",
            "sites",
            "site",
            "application",
            "virtualDirectory",
        };
        var attributes = new[]
        {
            new KeyValuePair<string, string>("physicalPath", path),
        };
        XmlUtils.ModifyOrCreateXmlAttributes(DestinationApplicationHostConfigFilePath, string.Empty, nodes, attributes);
    }

    private void SetSitePortInApplicationHostConfig()
    {
        var nodes = new[]
        {
            "configuration",
            "system.applicationHost",
            "sites",
            "site",
            "bindings",
            "binding",
        };
        var attributes = new[]
        {
            new KeyValuePair<string, string>("bindingInformation", string.Format(@"127.0.0.1:{0}:", Port)),
        };
        XmlUtils.ModifyOrCreateXmlAttributes(DestinationApplicationHostConfigFilePath, string.Empty, nodes, attributes);
    }

    private void SetApplicationPoolInApplicationHostConfig()
    {
        var appPoolName = @"IntegrationTestAppPool" + Port;

        var nodes = new[]
        {
            "configuration",
            "system.applicationHost",
            "applicationPools",
            "add",
        };
        var attributes = new[]
        {
            new KeyValuePair<string, string>("name", appPoolName),
        };
        XmlUtils.ModifyOrCreateXmlAttributes(DestinationApplicationHostConfigFilePath, string.Empty, nodes, attributes);

        nodes = new[]
        {
            "configuration",
            "system.applicationHost",
            "sites",
            "applicationDefaults",
        };
        attributes = new[]
        {
            new KeyValuePair<string, string>("applicationPool", appPoolName),
        };
        XmlUtils.ModifyOrCreateXmlAttributes(DestinationApplicationHostConfigFilePath, string.Empty, nodes, attributes);
    }
}
