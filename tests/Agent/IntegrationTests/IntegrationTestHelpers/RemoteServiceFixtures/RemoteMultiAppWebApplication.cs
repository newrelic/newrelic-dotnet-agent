// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using System.IO;

namespace NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;

/// <summary>
/// Hosts two copies of one ASP.NET Framework application as two application elements under a single
/// Hosted Web Core site and application pool: one process, two AppDomains, two agents. Application
/// one stays the site root ("/") and is staged entirely by the base class; this class adds only the
/// second application, mounted at SecondAppUrlPath.
/// </summary>
public class RemoteMultiAppWebApplication : RemoteWebApplication
{
    private const string MultiApplicationHostConfigFileName = @"applicationHost.MultiApp.config";

    private const string SecondAppPhysicalPathToken = "NEWRELIC_APP2_PHYSICAL_PATH";

    private readonly string _secondApplicationDirectoryName;

    /// <summary>
    /// The name the second application reports to the collector. Written into that copy's own
    /// Web.config, mirroring what the base class does with AppName for application one.
    /// </summary>
    public string SecondAppName { get; set; }

    /// <summary>
    /// URL path the second application is mounted at, with no leading slash. Must match the path
    /// attribute of the second application element in applicationHost.MultiApp.config.
    /// </summary>
    public string SecondAppUrlPath { get { return "app2"; } }

    public string DestinationSecondApplicationDirectoryPath { get { return Path.Combine(DestinationRootDirectoryPath, _secondApplicationDirectoryName); } }

    private string DestinationSecondApplicationWebConfigFilePath { get { return Path.Combine(DestinationSecondApplicationDirectoryPath, "Web.config"); } }

    private string DestinationMultiApplicationHostConfigFilePath { get { return Path.Combine(Path.GetDirectoryName(DestinationApplicationHostConfigFilePath), MultiApplicationHostConfigFileName); } }

    public RemoteMultiAppWebApplication(string applicationDirectoryName, ApplicationType applicationType)
        : base(applicationDirectoryName, applicationType)
    {
        _secondApplicationDirectoryName = applicationDirectoryName + "_app2";
        SecondAppName = _secondApplicationDirectoryName;
    }

    public override void CopyToRemote()
    {
        // Stages application one, copies the Hosted Web Core host directory (which brings
        // applicationHost.MultiApp.config with it), and reaches the override below through virtual
        // dispatch. Also appends HostedWebCore.exe to the newrelic.config process allow-list, which
        // is per-process and so is already correct for two applications.
        base.CopyToRemote();

        StageSecondApplication();
    }

    protected override void SetUpApplicationHostConfig()
    {
        InstallMultiApplicationHostConfig();

        // Sets application one's virtualDirectory physicalPath, the site binding, and the pool name.
        // Unchanged from the base behavior: the XmlUtils node walk lands on the FIRST matching child,
        // which is the root application, and the port and pool writes walk to different children.
        base.SetUpApplicationHostConfig();

        SetSecondApplicationPhysicalPath();
    }

    /// <summary>
    /// HostedWebCore.exe resolves its host config as AssemblyDirectory + "\applicationHost.config"
    /// (HostedWebCore.cs:35-42), so the two-application variant has to replace that file rather than
    /// be pointed at.
    /// </summary>
    private void InstallMultiApplicationHostConfig()
    {
        File.Copy(DestinationMultiApplicationHostConfigFilePath, DestinationApplicationHostConfigFilePath, true);
    }

    /// <summary>
    /// A token replace rather than an XML edit, because XmlUtils.ModifyOrCreateXmlAttributes can only
    /// reach the first matching node and so cannot target the second application element.
    /// </summary>
    private void SetSecondApplicationPhysicalPath()
    {
        var contents = File.ReadAllText(DestinationApplicationHostConfigFilePath);
        contents = contents.Replace(SecondAppPhysicalPathToken, DestinationSecondApplicationDirectoryPath);
        File.WriteAllText(DestinationApplicationHostConfigFilePath, contents);
    }

    private void StageSecondApplication()
    {
        Directory.CreateDirectory(DestinationSecondApplicationDirectoryPath);
        CommonUtils.CopyDirectory(SourceApplicationDirectoryPath, DestinationSecondApplicationDirectoryPath);

        SetSecondAppNameInWebConfig();
    }

    private void SetSecondAppNameInWebConfig()
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
            new KeyValuePair<string, string>("value", SecondAppName),
        };
        XmlUtils.ModifyOrCreateXmlAttributes(DestinationSecondApplicationWebConfigFilePath, string.Empty, nodes, attributes);
    }
}
