// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.IO;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;

namespace NewRelic.Agent.IntegrationTests.RemoteServiceFixtures
{
    /// <summary>
    /// Reuses the Owin2WebApi application, because explicitly names itself "RuleWebApi" (instead of a random guid name) so that specific transaction renaming rules can be set up ahead of time to support URL rule tests.
    /// </summary>
    public class RulesWebApi : RemoteApplicationFixture
    {
        private const string ApplicationDirectoryName = @"Owin2WebApi";
        private const string ExecutableName = @"Owin2WebApi.exe";
        private const string TargetFramework = "net451";

        public RulesWebApi()
            : base(new RemoteService(ApplicationDirectoryName, ExecutableName, TargetFramework, ApplicationType.Bounded))
        {
            Actions
            (
                setupConfiguration: () =>
                {
                    var newRelicConfigFilePath = DestinationNewRelicConfigFilePath;
                    var configModifier = new NewRelicConfigModifier(newRelicConfigFilePath);

                    CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(newRelicConfigFilePath, new[] { "configuration", "log" }, "level", "debug");
                    CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(newRelicConfigFilePath, new[] { "configuration", "requestParameters" }, "enabled", "true");

                    var appConfigFilePath = Path.Combine(RemoteApplication.DestinationApplicationDirectoryPath, ExecutableName) + ".config";
                    CommonUtils.SetAppNameInAppConfig(appConfigFilePath, "RulesWebApi");
                },
                exerciseApplication: () =>
                {
                    Sleep();
                    SegmentTerm();
                    UrlRule();
                }
            );
        }

        public void Sleep()
        {
            var address = string.Format("http://{0}:{1}/api/Sleep", DestinationServerName, Port);
            DownloadJsonAndAssertEqual(address, "Great success");
        }

        public void SegmentTerm()
        {
            var address = string.Format("http://{0}:{1}/api/SegmentTerm", DestinationServerName, Port);
            DownloadJsonAndAssertEqual(address, "Great success");
        }

        public void UrlRule()
        {
            var address = string.Format("http://{0}:{1}/api/UrlRule", DestinationServerName, Port);
            DownloadJsonAndAssertEqual(address, "Great success");
        }
    }
}
