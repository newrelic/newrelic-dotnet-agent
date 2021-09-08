// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Fixtures;
using NewRelic.Agent.Core.Labels;
using NewRelic.Agent.Core.Utilization;
using Newtonsoft.Json;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.DataTransport
{
    [TestFixture]
    public class ConnectModelTests
    {
        [Test]
        public void serializes_correctly()
        {
            var config = Mock.Create<IConfiguration>();

            using (new ConfigurationAutoResponder(config))
            {
                var utilitizationConfig = new UtilitizationConfig("my-host", 1, 2048);

                var vendors = new Dictionary<string, IVendorModel>()
                {
                    { "aws", new AwsVendorModel("myZone", "myInstanceId", "myInstanceType") },
                    { "azure", new AzureVendorModel("myLocation", "myName", "myVmId", "myVmSize") },
                    { "gcp" , new GcpVendorModel("myId", "myMachineType", "myName", "myZone") },
                    { "pcf", new PcfVendorModel("myInstanceGuid", "myInstanceIp", "myMemoryLimit") },
                    { "kubernetes", new KubernetesVendorModel("10.96.0.1") }
                };

                var connectModel = new ConnectModel
                    (
                    1,
                    "dotnet",
                    "customHostName",
                    "myHost",
                    new[] { "name1", "name2" },
                    "1.0",
                    0,
                    new SecuritySettingsModel(new TransactionTraceSettingsModel("raw")),
                    true,
                    "myIdentifier",
                    new[] { new Label("type1", "value1") },
                    new JavascriptAgentSettingsModel(true, "full"),
                    new Dictionary<string, string>(),
                    new UtilizationSettingsModel(2, 3, "myHost2", "myHost2.domain.com", new List<string> { "1.2.3.4", "5.6.7.8" }, null, vendors, utilitizationConfig),
                    null,
                    null,
                    null
                );

                var json = JsonConvert.SerializeObject(connectModel);

                const string expectedJson = @"{""pid"":1,""language"":""dotnet"",""display_host"":""customHostName"",""host"":""myHost"",""app_name"":[""name1"",""name2""],""agent_version"":""1.0"",""agent_version_timestamp"":0,""security_settings"":{""transaction_tracer"":{""record_sql"":""raw""}},""high_security"":true,""event_harvest_config"":null,""identifier"":""myIdentifier"",""labels"":[{""label_type"":""type1"",""label_value"":""value1""}],""settings"":{""browser_monitoring.loader_debug"":true,""browser_monitoring.loader"":""full""},""metadata"":{},""utilization"":{""metadata_version"":5,""logical_processors"":2,""total_ram_mib"":0,""hostname"":""myHost2"",""full_hostname"":""myHost2.domain.com"",""ip_address"":[""1.2.3.4"",""5.6.7.8""],""config"":{""hostname"":""my-host"",""logical_processors"":1,""total_ram_mib"":2048},""vendors"":{""aws"":{""availabilityZone"":""myZone"",""instanceId"":""myInstanceId"",""instanceType"":""myInstanceType""},""azure"":{""location"":""myLocation"",""name"":""myName"",""vmId"":""myVmId"",""vmSize"":""myVmSize""},""gcp"":{""id"":""myId"",""machineType"":""myMachineType"",""name"":""myName"",""zone"":""myZone""},""pcf"":{""cf_instance_guid"":""myInstanceGuid"",""cf_instance_ip"":""myInstanceIp"",""memory_limit"":""myMemoryLimit""},""kubernetes"":{""kubernetes_service_host"":""10.96.0.1""}}}}";

                Assert.AreEqual(expectedJson, json);
            }
        }
    }
}
