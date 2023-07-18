// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Api;
using NewRelic.Agent.Core.Utilization;
using NewRelic.Agent.TestUtilities;
using Newtonsoft.Json;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace CompositeTests.CrossAgentTests.Utilization
{
    [TestFixture]
    public class UtilizationCrossAgentTests
    {
        private const string PcfInstanceGuid = @"CF_INSTANCE_GUID";
        private const string PcfInstanceIp = @"CF_INSTANCE_IP";
        private const string PcfMemoryLimit = @"MEMORY_LIMIT";
        private const string KubernetesServiceHost = @"KUBERNETES_SERVICE_HOST";

        private static CompositeTestAgent _compositeTestAgent;
        private IAgent _agent;
        private VendorInfo _vendorInfo;

        public static List<TestCaseData> UtilizationTestDatas => GetUtilizationTestData();

        [SetUp]
        public void Setup()
        {
            _compositeTestAgent = new CompositeTestAgent();
            _agent = _compositeTestAgent.GetAgent();
            _vendorInfo = new VendorInfo(null, null, new NewRelic.SystemInterfaces.Environment(), null);
        }

        [TearDown]
        public static void TearDown()
        {
            System.Environment.SetEnvironmentVariable(PcfMemoryLimit, null, EnvironmentVariableTarget.Process);
            System.Environment.SetEnvironmentVariable(PcfInstanceIp, null, EnvironmentVariableTarget.Process);
            System.Environment.SetEnvironmentVariable(PcfInstanceGuid, null, EnvironmentVariableTarget.Process);
            System.Environment.SetEnvironmentVariable(KubernetesServiceHost, null, EnvironmentVariableTarget.Process);
            _compositeTestAgent.Dispose();

        }

        [TestCaseSource(nameof(UtilizationTestDatas))]
        public void Utilization_CrossAgentTests(UtilizationTestData testData)
        {
            var utilizationSettingsModel = new UtilizationSettingsModel(
                logicalProcessors: testData.InputLogicalProcessors.HasValue ? testData.InputLogicalProcessors.Value : (int?)null,
                totalRamBytes: testData.InputTotalRamMib.HasValue ? testData.InputTotalRamMib.Value * (1024 * 1024) : (ulong?)null,
                hostname: testData.InputHostname,
                fullHostName: testData.InputFullHostname,
                ipAddress: testData.InputIpAddress,
                bootId: null,
                vendors: PrepareVendorModels(testData),
                utilitizationConfig: PrepareUtilizationConfig(testData)
            );

            // Run Tests below

            var settings = new JsonSerializerSettings
            {
                TypeNameHandling = Newtonsoft.Json.TypeNameHandling.Auto,
                NullValueHandling = NullValueHandling.Ignore
            };
            var json = JsonConvert.SerializeObject(utilizationSettingsModel);
            Console.WriteLine($"JSON for '{testData.Testname}':\n {json}");
            var actualSettings = JsonConvert.DeserializeObject<ExpectedOutputJson>(json, settings);

            // Root values
            ValidateRootValues(actualSettings, testData.ExpectedOutputJson);

            // Vendors
            ValidateVendors(actualSettings, testData.ExpectedOutputJson);

            // User provided config settings
            ValidateUserConfigSettings(actualSettings, testData.ExpectedOutputJson);
        }

        private void ValidateRootValues(ExpectedOutputJson actualSettings, ExpectedOutputJson expectedOutput)
        {
            Assert.That(actualSettings.MetadataVersion, Is.EqualTo(expectedOutput.MetadataVersion));
            Assert.That(actualSettings.TotalRamMib, Is.EqualTo(expectedOutput.TotalRamMib));
            Assert.That(actualSettings.LogicalProcessors, Is.EqualTo(expectedOutput.LogicalProcessors));
            Assert.That(actualSettings.Hostname, Is.EqualTo(expectedOutput.Hostname));
            Assert.That(actualSettings.FullHostName, Is.EqualTo(expectedOutput.FullHostName));

            Assert.That(actualSettings.IpAddress.Count, Is.EqualTo(expectedOutput.IpAddress.Count));
            foreach (var ipAddress in expectedOutput.IpAddress)
            {
                Assert.That(actualSettings.IpAddress.Contains(ipAddress), Is.True);
            }
        }

        private void ValidateVendors(ExpectedOutputJson actualSettings, ExpectedOutputJson expectedOutput)
        {
            if (expectedOutput.Vendors != null)
            {
                // AWS
                if (expectedOutput.Vendors.Aws != null)
                {
                    Assert.That(actualSettings.Vendors.Aws.InstanceId, Is.EqualTo(expectedOutput.Vendors.Aws.InstanceId));
                    Assert.That(actualSettings.Vendors.Aws.InstanceType, Is.EqualTo(expectedOutput.Vendors.Aws.InstanceType));
                    Assert.That(actualSettings.Vendors.Aws.AvailabilityZone, Is.EqualTo(expectedOutput.Vendors.Aws.AvailabilityZone));
                }
                else
                {
                    Assert.That(actualSettings.Vendors?.Aws, Is.Null);
                }

                // Azure
                if (expectedOutput.Vendors.Azure != null)
                {
                    Assert.That(actualSettings.Vendors.Azure.Location, Is.EqualTo(expectedOutput.Vendors.Azure.Location));
                    Assert.That(actualSettings.Vendors.Azure.VmId, Is.EqualTo(expectedOutput.Vendors.Azure.VmId));
                    Assert.That(actualSettings.Vendors.Azure.VmSize, Is.EqualTo(expectedOutput.Vendors.Azure.VmSize));
                }
                else
                {
                    Assert.That(actualSettings.Vendors?.Azure, Is.Null);
                }

                // GCP
                if (expectedOutput.Vendors.Gcp != null)
                {
                    Assert.That(actualSettings.Vendors.Gcp.Id, Is.EqualTo(expectedOutput.Vendors.Gcp.Id));
                    Assert.That(actualSettings.Vendors.Gcp.MachineType, Is.EqualTo(expectedOutput.Vendors.Gcp.MachineType));
                    Assert.That(actualSettings.Vendors.Gcp.Zone, Is.EqualTo(expectedOutput.Vendors.Gcp.Zone));
                }
                else
                {
                    Assert.That(actualSettings.Vendors?.Gcp, Is.Null);
                }

                // PCF
                if (expectedOutput.Vendors.Pcf != null)
                {
                    Assert.That(actualSettings.Vendors.Pcf.CfInstanceGuid, Is.EqualTo(expectedOutput.Vendors.Pcf.CfInstanceGuid));
                    Assert.That(actualSettings.Vendors.Pcf.CfInstanceIp, Is.EqualTo(expectedOutput.Vendors.Pcf.CfInstanceIp));
                    Assert.That(actualSettings.Vendors.Pcf.MemoryLimit, Is.EqualTo(expectedOutput.Vendors.Pcf.MemoryLimit));
                }
                else
                {
                    Assert.That(actualSettings.Vendors?.Pcf, Is.Null);
                }

                //Kubernbetes
                if (expectedOutput.Vendors.Kubernetes != null)
                {
                    Assert.That(actualSettings.Vendors.Kubernetes.KubernetesServiceHost, Is.EqualTo(expectedOutput.Vendors.Kubernetes.KubernetesServiceHost));
                }
                else
                {
                    Assert.That(actualSettings.Vendors?.Kubernetes, Is.Null);
                }
            }
            else
            {
                Assert.That(actualSettings.Vendors, Is.Null);
            }
        }

        private void ValidateUserConfigSettings(ExpectedOutputJson actualSettings, ExpectedOutputJson expectedOutput)
        {
            if (expectedOutput.Config != null)
            {
                // Logical Processors
                if (expectedOutput.Config.LogicalProcessors != null)
                {
                    Assert.That(actualSettings.Config.LogicalProcessors, Is.EqualTo(expectedOutput.Config.LogicalProcessors));
                }
                else
                {
                    Assert.That(actualSettings.Config.LogicalProcessors, Is.Null);
                }


                // Total RAM Mib
                if (expectedOutput.Config.TotalRamMib != null)
                {
                    Assert.That(actualSettings.Config.TotalRamMib, Is.EqualTo(expectedOutput.Config.TotalRamMib));
                }
                else
                {
                    Assert.That(actualSettings.Config.TotalRamMib, Is.Null);
                }

                // Billing_Hostname
                if (expectedOutput.Config.Hostname != null)
                {
                    Assert.That(actualSettings.Config.Hostname, Is.EqualTo(expectedOutput.Config.Hostname));
                }
                else
                {
                    Assert.That(actualSettings.Config.Hostname, Is.Null);
                }
            }
            else
            {
                Assert.That(actualSettings.Config, Is.Null);
            }
        }

        private static List<TestCaseData> GetUtilizationTestData()
        {
            var testCaseDatas = new List<TestCaseData>();

            string location = Assembly.GetExecutingAssembly().GetLocation();
            var dllPath = Path.GetDirectoryName(new Uri(location).LocalPath);
            var jsonPath = Path.Combine(dllPath, "CrossAgentTests", "Utilization", "utilization_json.json");
            var jsonString = File.ReadAllText(jsonPath);

            var settings = new JsonSerializerSettings
            {
                TypeNameHandling = Newtonsoft.Json.TypeNameHandling.Auto,
                Error = (sender, args) =>
                {
                    if (System.Diagnostics.Debugger.IsAttached)
                    {
                        System.Diagnostics.Debugger.Break();
                    }
                }
            };
            var testList = JsonConvert.DeserializeObject<List<UtilizationTestData>>(jsonString, settings);

            foreach (var testData in testList)
            {
                var testCase = new TestCaseData(testData);
                testCase.SetName("UtilizationCrossAgentTests: " + testData.Testname);
                testCaseDatas.Add(testCase);
            }

            return testCaseDatas;
        }

        private UtilitizationConfig PrepareUtilizationConfig(UtilizationTestData testData)
        {
            if (testData.InputEnvironmentVariables == null)
            {
                return null;
            }

            int? logicalProcessors = null;
            if (int.TryParse(testData.InputEnvironmentVariables.NewRelicUtilizationLogicalProcessors.ToString(), out var valueLP))
            {
                logicalProcessors = valueLP;
            }

            int? totalRamMib = null;
            if (int.TryParse(testData.InputEnvironmentVariables.NewRelicUtilizationTotalRamMib.ToString(), out var valueTRM))
            {
                totalRamMib = valueTRM;
            }


            return new UtilitizationConfig(
                testData.InputEnvironmentVariables.NewRelicUtilizationBillingHostname,
                logicalProcessors,
                totalRamMib
            );
        }

        private Dictionary<string, IVendorModel> PrepareVendorModels(UtilizationTestData testData)
        {
            var awsVendor = GetAwsVendorModel(testData);
            var azureVendor = GetAzureVendorModel(testData);
            var gcpVendor = GetGcpVendorModel(testData);
            var pcfVendor = GetPcfVendorModel(testData);
            var kubernetesVendor = GetKubernetesVendorModel(testData);

            var vendors = new Dictionary<string, IVendorModel>();

            if (awsVendor != null)
            {
                vendors.Add("aws", awsVendor);
            }

            if (azureVendor != null)
            {
                vendors.Add("azure", azureVendor);
            }

            if (gcpVendor != null)
            {
                vendors.Add("gcp", gcpVendor);
            }

            if (pcfVendor != null)
            {
                vendors.Add("pcf", pcfVendor);
            }

            if (kubernetesVendor != null)
            {
                vendors.Add("kubernetes", kubernetesVendor);
            }

            return vendors;
        }

        private AwsVendorModel GetAwsVendorModel(UtilizationTestData testData)
        {
            var model = new AwsVendorModel(testData.InputAwsZone, testData.InputAwsId, testData.InputAwsType);
            var json = JsonConvert.SerializeObject(model);
            return (AwsVendorModel)_vendorInfo.ParseAwsVendorInfo(json);
        }

        private AzureVendorModel GetAzureVendorModel(UtilizationTestData testData)
        {
            var model = new AzureVendorModel(testData.InputAzureLocation, testData.InputAzureName, testData.InputAzureId, testData.InputAzureSize);
            var json = JsonConvert.SerializeObject(model);
            return (AzureVendorModel)_vendorInfo.ParseAzureVendorInfo(json);
        }

        private GcpVendorModel GetGcpVendorModel(UtilizationTestData testData)
        {
            var model = new GcpVendorModel(testData.InputGcpId, testData.InputGcpType, testData.InputGcpName, testData.InputGcpZone);
            var json = JsonConvert.SerializeObject(model);
            return (GcpVendorModel)_vendorInfo.ParseGcpVendorInfo(json);
        }

        private PcfVendorModel GetPcfVendorModel(UtilizationTestData testData)
        {
            if (!string.IsNullOrEmpty(testData.InputPcfGuid))
            {
                System.Environment.SetEnvironmentVariable(PcfInstanceGuid, testData.InputPcfGuid, EnvironmentVariableTarget.Process);
            }

            if (!string.IsNullOrEmpty(testData.InputPcfIp))
            {
                System.Environment.SetEnvironmentVariable(PcfInstanceIp, testData.InputPcfIp, EnvironmentVariableTarget.Process);
            }

            if (!string.IsNullOrEmpty(testData.InputPcfMemLimit))
            {
                System.Environment.SetEnvironmentVariable(PcfMemoryLimit, testData.InputPcfMemLimit, EnvironmentVariableTarget.Process);
            }

            return (PcfVendorModel)_vendorInfo.GetPcfVendorInfo();
        }

        private KubernetesVendorModel GetKubernetesVendorModel(UtilizationTestData testData)
        {
            if (!string.IsNullOrEmpty(testData.InputEnvironmentVariables?.KubernetesServiceHost))
            {
                System.Environment.SetEnvironmentVariable(KubernetesServiceHost, testData.InputEnvironmentVariables.KubernetesServiceHost, EnvironmentVariableTarget.Process);
            }

            return (KubernetesVendorModel)_vendorInfo.GetKubernetesInfo();
        }

        #region Test Data

        public class UtilizationTestData
        {
            [JsonProperty("testname")]
            public string Testname { get; set; }

            [JsonProperty("input_total_ram_mib", NullValueHandling = NullValueHandling.Include)]
            public ulong? InputTotalRamMib { get; set; }

            [JsonProperty("input_logical_processors", NullValueHandling = NullValueHandling.Include)]
            public int? InputLogicalProcessors { get; set; }

            [JsonProperty("input_hostname", NullValueHandling = NullValueHandling.Include)]
            public string InputHostname { get; set; }

            [JsonProperty("input_full_hostname")]
            public string InputFullHostname { get; set; }

            [JsonProperty("input_ip_address")]
            public List<string> InputIpAddress { get; set; }

            [JsonProperty("expected_output_json")]
            public ExpectedOutputJson ExpectedOutputJson { get; set; }

            [JsonProperty("input_environment_variables", NullValueHandling = NullValueHandling.Ignore)]
            public InputEnvironmentVariables InputEnvironmentVariables { get; set; }

            [JsonProperty("input_aws_id", NullValueHandling = NullValueHandling.Ignore)]
            public string InputAwsId { get; set; }

            [JsonProperty("input_aws_type", NullValueHandling = NullValueHandling.Ignore)]
            public string InputAwsType { get; set; }

            [JsonProperty("input_aws_zone", NullValueHandling = NullValueHandling.Ignore)]
            public string InputAwsZone { get; set; }

            [JsonProperty("input_gcp_id", NullValueHandling = NullValueHandling.Ignore)]
            public string InputGcpId { get; set; }

            [JsonProperty("input_gcp_type", NullValueHandling = NullValueHandling.Ignore)]
            public string InputGcpType { get; set; }

            [JsonProperty("input_gcp_name", NullValueHandling = NullValueHandling.Ignore)]
            public string InputGcpName { get; set; }

            [JsonProperty("input_gcp_zone", NullValueHandling = NullValueHandling.Ignore)]
            public string InputGcpZone { get; set; }

            [JsonProperty("input_pcf_guid", NullValueHandling = NullValueHandling.Ignore)]
            public string InputPcfGuid { get; set; }

            [JsonProperty("input_pcf_ip", NullValueHandling = NullValueHandling.Ignore)]
            public string InputPcfIp { get; set; }

            [JsonProperty("input_pcf_mem_limit", NullValueHandling = NullValueHandling.Ignore)]
            public string InputPcfMemLimit { get; set; }

            [JsonProperty("input_azure_location", NullValueHandling = NullValueHandling.Ignore)]
            public string InputAzureLocation { get; set; }

            [JsonProperty("input_azure_name", NullValueHandling = NullValueHandling.Ignore)]
            public string InputAzureName { get; set; }

            [JsonProperty("input_azure_id", NullValueHandling = NullValueHandling.Ignore)]
            public string InputAzureId { get; set; }

            [JsonProperty("input_azure_size", NullValueHandling = NullValueHandling.Ignore)]
            public string InputAzureSize { get; set; }
        }

        public class ExpectedOutputJson
        {
            [JsonProperty("metadata_version")]
            public long MetadataVersion { get; set; }

            [JsonProperty("logical_processors", NullValueHandling = NullValueHandling.Include)]
            public long? LogicalProcessors { get; set; }

            [JsonProperty("total_ram_mib", NullValueHandling = NullValueHandling.Include)]
            public long? TotalRamMib { get; set; }

            [JsonProperty("hostname", NullValueHandling = NullValueHandling.Include)]
            public string Hostname { get; set; }

            [JsonProperty("full_hostname")]
            public string FullHostName { get; set; }

            [JsonProperty("ip_address")]
            public List<string> IpAddress { get; set; }

            [JsonProperty("config", NullValueHandling = NullValueHandling.Ignore)]
            public Config Config { get; set; }

            [JsonProperty("vendors", NullValueHandling = NullValueHandling.Ignore)]
            public Vendors Vendors { get; set; }
        }

        public class Config
        {
            [JsonProperty("logical_processors", NullValueHandling = NullValueHandling.Ignore)]
            public long? LogicalProcessors { get; set; }

            [JsonProperty("total_ram_mib", NullValueHandling = NullValueHandling.Ignore)]
            public long? TotalRamMib { get; set; }

            [JsonProperty("hostname", NullValueHandling = NullValueHandling.Ignore)]
            public string Hostname { get; set; }
        }

        public partial class Vendors
        {
            [JsonProperty("aws", NullValueHandling = NullValueHandling.Ignore)]
            public Aws Aws { get; set; }

            [JsonProperty("gcp", NullValueHandling = NullValueHandling.Ignore)]
            public Gcp Gcp { get; set; }

            [JsonProperty("pcf", NullValueHandling = NullValueHandling.Ignore)]
            public Pcf Pcf { get; set; }

            [JsonProperty("azure", NullValueHandling = NullValueHandling.Ignore)]
            public Azure Azure { get; set; }

            [JsonProperty("kubernetes", NullValueHandling = NullValueHandling.Ignore)]
            public Kubernetes Kubernetes { get; set; }
        }

        public class Aws
        {
            [JsonProperty("instanceId", NullValueHandling = NullValueHandling.Ignore)]
            public string InstanceId { get; set; }

            [JsonProperty("instanceType", NullValueHandling = NullValueHandling.Ignore)]
            public string InstanceType { get; set; }

            [JsonProperty("availabilityZone", NullValueHandling = NullValueHandling.Ignore)]
            public string AvailabilityZone { get; set; }
        }

        public class Azure
        {
            [JsonProperty("location", NullValueHandling = NullValueHandling.Ignore)]
            public string Location { get; set; }

            [JsonProperty("name", NullValueHandling = NullValueHandling.Ignore)]
            public string Name { get; set; }

            [JsonProperty("vmId", NullValueHandling = NullValueHandling.Ignore)]
            public string VmId { get; set; }

            [JsonProperty("vmSize", NullValueHandling = NullValueHandling.Ignore)]
            public string VmSize { get; set; }
        }

        public class Gcp
        {
            [JsonProperty("id", NullValueHandling = NullValueHandling.Ignore)]
            public string Id { get; set; }

            [JsonProperty("machineType", NullValueHandling = NullValueHandling.Ignore)]
            public string MachineType { get; set; }

            [JsonProperty("name", NullValueHandling = NullValueHandling.Ignore)]
            public string Name { get; set; }

            [JsonProperty("zone", NullValueHandling = NullValueHandling.Ignore)]
            public string Zone { get; set; }
        }

        public class Pcf
        {
            [JsonProperty("cf_instance_guid", NullValueHandling = NullValueHandling.Ignore)]
            public string CfInstanceGuid { get; set; }

            [JsonProperty("cf_instance_ip", NullValueHandling = NullValueHandling.Ignore)]
            public string CfInstanceIp { get; set; }

            [JsonProperty("memory_limit", NullValueHandling = NullValueHandling.Ignore)]
            public string MemoryLimit { get; set; }
        }

        public class Kubernetes
        {
            [JsonProperty("kubernetes_service_host", NullValueHandling = NullValueHandling.Ignore)]
            public string KubernetesServiceHost { get; set; }
        }

        public class InputEnvironmentVariables
        {
            [JsonProperty("NEW_RELIC_UTILIZATION_LOGICAL_PROCESSORS")]
            public object NewRelicUtilizationLogicalProcessors { get; set; }

            [JsonProperty("NEW_RELIC_UTILIZATION_TOTAL_RAM_MIB")]
            public object NewRelicUtilizationTotalRamMib { get; set; }

            [JsonProperty("NEW_RELIC_UTILIZATION_BILLING_HOSTNAME", NullValueHandling = NullValueHandling.Ignore)]
            public string NewRelicUtilizationBillingHostname { get; set; }

            [JsonProperty("KUBERNETES_SERVICE_HOST", NullValueHandling = NullValueHandling.Ignore)]
            public string KubernetesServiceHost { get; set; }

            [JsonProperty("KUBERNETES_SERVICE_PORT", NullValueHandling = NullValueHandling.Ignore)]
            public long? KubernetesServicePort { get; set; }
        }

        #endregion
    }
}
