// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using NewRelic.Agent.Configuration;
using Newtonsoft.Json;
using NUnit.Framework;

namespace NewRelic.Agent.Core.Utilization
{
    public class UtilizationSettingsTest
    {
        [Test]
        public void when_default_fixture_values_are_used_then_serializes_correctly()
        {
            var vendors = new Dictionary<string, IVendorModel>
            {
                { "aws", new AwsVendorModel("myZone", "myInstanceId", "myInstanceType") },
                { "azure", new AzureVendorModel("myLocation", "myName", "myVmId", "myVmSize") },
                { "gcp" , new GcpVendorModel("myId", "myMachineType", "myName", "myZone") },
                { "pcf", new PcfVendorModel("myInstanceGuid", "myInstanceIp", "myMemoryLimit") },
                { "docker", new DockerVendorModel("myBootId") },
                { "kubernetes", new KubernetesVendorModel("10.96.0.1") }
            };
            var config = new UtilitizationConfig("loc-alhost", 2, 2048);
            var settingsModel = new UtilizationSettingsModel(4, 1024 * 1024 * 1024, "lo-calhost", "lo-calhost.domain.com", new List<string> { "1.2.3.4", "5.6.7.8" }, null, vendors, config);

            // ACT
            var actualJson = JsonConvert.SerializeObject(settingsModel);

            var expectedObject = new Dictionary<string, object>
            {
                {"metadata_version", 5},
                {"logical_processors", 4},
                {"total_ram_mib", 1024},
                {"hostname", "lo-calhost"},
                {"full_hostname", "lo-calhost.domain.com"},
                {"ip_address", new[] { "1.2.3.4","5.6.7.8" }},
                {
                    "config",  new Dictionary<string, object>
                    {
                        {"hostname", "loc-alhost"},
                        {"logical_processors", 2},
                        {"total_ram_mib", 2048}
                    }
                },
                {
                    "vendors", new Dictionary<string, object>
                    {
                        {
                            "aws", new Dictionary<string, object>
                            {
                                {"availabilityZone", "myZone"},
                                {"instanceId", "myInstanceId"},
                                {"instanceType", "myInstanceType"}
                            }
                        },
                        {
                            "azure", new Dictionary<string,object>
                            {
                                {"location", "myLocation" },
                                {"name", "myName" },
                                {"vmId", "myVmId" },
                                {"vmSize", "myVmSize" }
                            }
                        },
                        {
                            "gcp", new Dictionary<string,object>
                            {
                                {"id", "myId" },
                                {"machineType", "myMachineType" },
                                {"name", "myName" },
                                {"zone", "myZone" }
                            }
                        },
                        {
                            "pcf", new Dictionary<string,object>
                            {
                                {"cf_instance_guid", "myInstanceGuid" },
                                {"cf_instance_ip", "myInstanceIp" },
                                {"memory_limit", "myMemoryLimit" }
                            }
                        },
                        {
                            "docker", new Dictionary<string,object>
                            {
                                {"id", "myBootId" }
                            }
                        },
                        {
                            "kubernetes", new Dictionary<string,object>
                            {
                                {"kubernetes_service_host", "10.96.0.1" }
                            }
                        }
                    }
                }
            };
            var expectedJson = JsonConvert.SerializeObject(expectedObject);
            Assert.That(actualJson, Is.EqualTo(expectedJson), string.Format("Expected {0}, but was {1}.", expectedJson, JsonConvert.SerializeObject(settingsModel)));
        }

        [Test]
        public void when_vendors_contain_null_values_serializes_correctly()
        {
            var vendors = new Dictionary<string, IVendorModel>
            {
                { "aws", new AwsVendorModel(null, "myInstanceId", "myInstanceType") },
                { "azure", new AzureVendorModel("myLocation", null, "myVmId", "myVmSize") },
                { "gcp" , new GcpVendorModel("myId", "myMachineType", "myName", null) },
                { "pcf", new PcfVendorModel("myInstanceGuid", null, "myMemoryLimit") },
                { "docker", new DockerVendorModel("myBootId") }
            };
            var config = new UtilitizationConfig("loc-alhost", 2, 2048);
            var settingsModel = new UtilizationSettingsModel(4, 1024 * 1024 * 1024, "lo-calhost", "lo-calhost.domain.com", new List<string> { "1.2.3.4", "5.6.7.8" }, null, vendors, config);

            // ACT
            var actualJson = JsonConvert.SerializeObject(settingsModel);

            var expectedObject = new Dictionary<string, object>
            {
                {"metadata_version", 5},
                {"logical_processors", 4},
                {"total_ram_mib", 1024},
                {"hostname", "lo-calhost"},
                {"full_hostname", "lo-calhost.domain.com"},
                {"ip_address", new[] { "1.2.3.4","5.6.7.8" }},
                {
                    "config",  new Dictionary<string, object>
                    {
                        {"hostname", "loc-alhost"},
                        {"logical_processors", 2},
                        {"total_ram_mib", 2048}
                    }
                },
                {
                    "vendors", new Dictionary<string, object>
                    {
                        {
                            "aws", new Dictionary<string, object>
                            {
                                {"instanceId", "myInstanceId"},
                                {"instanceType", "myInstanceType"}
                            }
                        },
                        {
                            "azure", new Dictionary<string,object>
                            {
                                {"location", "myLocation" },
                                {"vmId", "myVmId" },
                                {"vmSize", "myVmSize" }
                            }
                        },
                        {
                            "gcp", new Dictionary<string,object>
                            {
                                {"id", "myId" },
                                {"machineType", "myMachineType" },
                                {"name", "myName" },
                            }
                        },
                        {
                            "pcf", new Dictionary<string,object>
                            {
                                {"cf_instance_guid", "myInstanceGuid" },
                                {"memory_limit", "myMemoryLimit" }
                            }
                        },
                        {
                            "docker", new Dictionary<string,object>
                            {
                                {"id", "myBootId" }
                            }
                        }
                    }
                }
            };
            var expectedJson = JsonConvert.SerializeObject(expectedObject);
            Assert.That(actualJson, Is.EqualTo(expectedJson), string.Format("Expected {0}, but was {1}.", expectedJson, JsonConvert.SerializeObject(settingsModel)));
        }

        [Test]
        public void when_no_vendors_then_serializes_correctly()
        {
            var vendors = new Dictionary<string, IVendorModel>();
            var config = new UtilitizationConfig("loc-alhost", 2, 2048);
            var settingsModel = new UtilizationSettingsModel(4, 1024 * 1024 * 1024, "lo-calhost", "lo-calhost.domain.com", new List<string> { "1.2.3.4", "5.6.7.8" }, null, vendors, config);

            // ACT
            var actualJson = JsonConvert.SerializeObject(settingsModel);

            var expectedObject = new Dictionary<string, object>
            {
                {"metadata_version", 5},
                {"logical_processors", 4},
                {"total_ram_mib", 1024},
                {"hostname", "lo-calhost"},
                {"full_hostname", "lo-calhost.domain.com"},
                {"ip_address", new[] { "1.2.3.4","5.6.7.8" }},
                {
                    "config",  new Dictionary<string, object>
                    {
                        {"hostname", "loc-alhost"},
                        {"logical_processors", 2},
                        {"total_ram_mib", 2048}
                    }
                }
            };
            var expectedJson = JsonConvert.SerializeObject(expectedObject);
            Assert.That(actualJson, Is.EqualTo(expectedJson), string.Format("Expected {0}, but was {1}.", expectedJson, JsonConvert.SerializeObject(settingsModel)));

        }

        [Test]
        public void UtilizationHashWithNullIConfigurationSerializesCorrectly()
        {
            var vendors = new Dictionary<string, IVendorModel>();
            UtilitizationConfig config = GetUtilitizationConfig(null);
            var settingsModel = new UtilizationSettingsModel(4, 1024 * 1024 * 1024, "lo-calhost", "lo-calhost.domain.com", new List<string> { "1.2.3.4", "5.6.7.8" }, null, vendors, config);

            // ACT
            var actualJson = JsonConvert.SerializeObject(settingsModel);

            var expectedObject = new Dictionary<string, object>
            {
                {"metadata_version", 5},
                {"logical_processors", 4},
                {"total_ram_mib", 1024},
                {"hostname", "lo-calhost"},
                {"full_hostname", "lo-calhost.domain.com"},
                {"ip_address", new[] { "1.2.3.4","5.6.7.8" }}
            };
            var expectedJson = JsonConvert.SerializeObject(expectedObject);
            Assert.That(actualJson, Is.EqualTo(expectedJson), string.Format("Expected {0}, but was {1}.", expectedJson, JsonConvert.SerializeObject(settingsModel)));
        }

        [Test]
        public void UtilizationHashWithNullEntriesSerializesCorrectly()
        {
            var vendors = new Dictionary<string, IVendorModel>();
            UtilitizationConfig config = GetUtilitizationConfig(null, null, null);
            var settingsModel = new UtilizationSettingsModel(4, 1024 * 1024 * 1024, "lo-calhost", "lo-calhost.domain.com", new List<string> { "1.2.3.4", "5.6.7.8" }, null, vendors, config);

            // ACT
            var actualJson = JsonConvert.SerializeObject(settingsModel);

            var expectedObject = new Dictionary<string, object>
            {
                {"metadata_version", 5},
                {"logical_processors", 4},
                {"total_ram_mib", 1024},
                {"hostname", "lo-calhost"},
                {"full_hostname", "lo-calhost.domain.com"},
                {"ip_address", new[] { "1.2.3.4","5.6.7.8" }},
            };
            var expectedJson = JsonConvert.SerializeObject(expectedObject);
            Assert.That(actualJson, Is.EqualTo(expectedJson), string.Format("Expected {0}, but was {1}.", expectedJson, JsonConvert.SerializeObject(settingsModel)));
        }

        [Test]
        public void UtilizationHashWithAllConfigHashEntriesSerializesCorrectly()
        {
            var vendors = new Dictionary<string, IVendorModel>();
            var config = new UtilitizationConfig("loc-alhost", 2, 2048);
            var settingsModel = new UtilizationSettingsModel(4, 1024 * 1024 * 1024, "lo-calhost", "lo-calhost.domain.com", new List<string> { "1.2.3.4", "5.6.7.8" }, null, vendors, config);

            // ACT
            var actualJson = JsonConvert.SerializeObject(settingsModel);

            var expectedObject = new Dictionary<string, object>
            {
                {"metadata_version", 5},
                {"logical_processors", 4},
                {"total_ram_mib", 1024},
                {"hostname", "lo-calhost"},
                {"full_hostname", "lo-calhost.domain.com"},
                {"ip_address", new[] { "1.2.3.4","5.6.7.8" }},
                {
                    "config",  new Dictionary<string, object>
                    {
                        {"hostname", "loc-alhost"},
                        {"logical_processors", 2},
                        {"total_ram_mib", 2048}
                    }
                }
            };
            var expectedJson = JsonConvert.SerializeObject(expectedObject);
            Assert.That(actualJson, Is.EqualTo(expectedJson), string.Format("Expected {0}, but was {1}.", expectedJson, JsonConvert.SerializeObject(settingsModel)));
        }

        [Test]
        public void UtilizationHashWithSingleConfigHashEntrySerializesCorrectly()
        {
            var vendors = new Dictionary<string, IVendorModel>();
            var config = new UtilitizationConfig(null, null, 2048);
            var settingsModel = new UtilizationSettingsModel(4, 1024 * 1024 * 1024, "lo-calhost", "lo-calhost.domain.com", new List<string> { "1.2.3.4", "5.6.7.8" }, null, vendors, config);

            // ACT
            var actualJson = JsonConvert.SerializeObject(settingsModel);

            var expectedObject = new Dictionary<string, object>
            {
                {"metadata_version", 5},
                {"logical_processors", 4},
                {"total_ram_mib", 1024},
                {"hostname", "lo-calhost"},
                {"full_hostname", "lo-calhost.domain.com"},
                {"ip_address", new[] { "1.2.3.4","5.6.7.8" }},
                {
                    "config",  new Dictionary<string, object>
                    {
                        {"total_ram_mib", 2048}
                    }
                }
            };
            var expectedJson = JsonConvert.SerializeObject(expectedObject);
            Assert.That(actualJson, Is.EqualTo(expectedJson), string.Format("Expected {0}, but was {1}.", expectedJson, JsonConvert.SerializeObject(settingsModel)));
        }

        //Same method used in main code, except this one take the config vs having it be global.
        private UtilitizationConfig GetUtilitizationConfig(IConfiguration configuration)
        {
            if (configuration == null)
            {
                return null;
            }

            if (string.IsNullOrEmpty(configuration.UtilizationBillingHost)
                && configuration.UtilizationLogicalProcessors == null
                && configuration.UtilizationTotalRamMib == null)
            {
                return null;
            }

            return new UtilitizationConfig(configuration.UtilizationBillingHost, configuration.UtilizationLogicalProcessors, configuration.UtilizationTotalRamMib);
        }

        //Same method used in main code, except this one take the config vs having it be global.
        private UtilitizationConfig GetUtilitizationConfig(string billingHost, int? logicalProcessors, int? totalRamMib)
        {
            if (string.IsNullOrEmpty(billingHost)
                && logicalProcessors == null
                && totalRamMib == null)
            {
                return null;
            }

            return new UtilitizationConfig(billingHost, logicalProcessors, totalRamMib);
        }
    }
}
