using System.Collections.Generic;
using System.Linq;
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
            var vendors = new List<IVendorModel>
            {
                new AwsVendorModel("123456", "t2.micro", "us-west-1")
            };
            var config = new UtilitizationConfig("loc-alhost", 2, 2048);
            var settingsModel = new UtilizationSettingsModel(4, 1024 * 1024 * 1024, "lo-calhost", null, vendors, config);

            // ACT
            var actualJson = JsonConvert.SerializeObject(settingsModel);

            var expectedObject = new Dictionary<string, object>
            {
                {"metadata_version", 3},
                {"logical_processors", 4},
                {"total_ram_mib", 1024},
                {"hostname", "lo-calhost"},
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
                                {"id", "123456"},
                                {"type", "t2.micro"},
                                {"zone", "us-west-1"}
                            }
                        }
                    }
                }
            };
            var expectedJson = JsonConvert.SerializeObject(expectedObject);
            Assert.AreEqual(expectedJson, actualJson, string.Format("Expected {0}, but was {1}.", expectedJson, JsonConvert.SerializeObject(settingsModel)));

        }

        [Test]
        public void when_no_vendors_then_serializes_correctly()
        {
            var vendors = Enumerable.Empty<IVendorModel>();
            var config = new UtilitizationConfig("loc-alhost", 2, 2048);
            var settingsModel = new UtilizationSettingsModel(4, 1024 * 1024 * 1024, "lo-calhost", null, vendors, config);

            // ACT
            var actualJson = JsonConvert.SerializeObject(settingsModel);

            var expectedObject = new Dictionary<string, object>
            {
                {"metadata_version", 3},
                {"logical_processors", 4},
                {"total_ram_mib", 1024},
                {"hostname", "lo-calhost"},
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
            Assert.AreEqual(expectedJson, actualJson, string.Format("Expected {0}, but was {1}.", expectedJson, JsonConvert.SerializeObject(settingsModel)));

        }

        [Test]
        public void UtilizationHashWithNullIConfigurationSerializesCorrectly()
        {
            var vendors = Enumerable.Empty<IVendorModel>();
            UtilitizationConfig config = GetUtilitizationConfig(null);
            var settingsModel = new UtilizationSettingsModel(4, 1024 * 1024 * 1024, "lo-calhost", null, vendors, config);

            // ACT
            var actualJson = JsonConvert.SerializeObject(settingsModel);

            var expectedObject = new Dictionary<string, object>
            {
                {"metadata_version", 3},
                {"logical_processors", 4},
                {"total_ram_mib", 1024},
                {"hostname", "lo-calhost"}
            };
            var expectedJson = JsonConvert.SerializeObject(expectedObject);
            Assert.AreEqual(expectedJson, actualJson, string.Format("Expected {0}, but was {1}.", expectedJson, JsonConvert.SerializeObject(settingsModel)));
        }

        [Test]
        public void UtilizationHashWithNullEntriesSerializesCorrectly()
        {
            var vendors = Enumerable.Empty<IVendorModel>();
            UtilitizationConfig config = GetUtilitizationConfig(null, null, null);
            var settingsModel = new UtilizationSettingsModel(4, 1024 * 1024 * 1024, "lo-calhost", null, vendors, config);

            // ACT
            var actualJson = JsonConvert.SerializeObject(settingsModel);

            var expectedObject = new Dictionary<string, object>
            {
                {"metadata_version", 3},
                {"logical_processors", 4},
                {"total_ram_mib", 1024},
                {"hostname", "lo-calhost"}
            };
            var expectedJson = JsonConvert.SerializeObject(expectedObject);
            Assert.AreEqual(expectedJson, actualJson, string.Format("Expected {0}, but was {1}.", expectedJson, JsonConvert.SerializeObject(settingsModel)));
        }

        [Test]
        public void UtilizationHashWithAllConfigHashEntriesSerializesCorrectly()
        {
            var vendors = Enumerable.Empty<IVendorModel>();
            var config = new UtilitizationConfig("loc-alhost", 2, 2048);
            var settingsModel = new UtilizationSettingsModel(4, 1024 * 1024 * 1024, "lo-calhost", null, vendors, config);

            // ACT
            var actualJson = JsonConvert.SerializeObject(settingsModel);

            var expectedObject = new Dictionary<string, object>
            {
                {"metadata_version", 3},
                {"logical_processors", 4},
                {"total_ram_mib", 1024},
                {"hostname", "lo-calhost"},
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
            Assert.AreEqual(expectedJson, actualJson, string.Format("Expected {0}, but was {1}.", expectedJson, JsonConvert.SerializeObject(settingsModel)));
        }

        [Test]
        public void UtilizationHashWithSingleConfigHashEntrySerializesCorrectly()
        {
            var vendors = Enumerable.Empty<IVendorModel>();
            var config = new UtilitizationConfig(null, null, 2048);
            var settingsModel = new UtilizationSettingsModel(4, 1024 * 1024 * 1024, "lo-calhost", null, vendors, config);

            // ACT
            var actualJson = JsonConvert.SerializeObject(settingsModel);

            var expectedObject = new Dictionary<string, object>
            {
                {"metadata_version", 3},
                {"logical_processors", 4},
                {"total_ram_mib", 1024},
                {"hostname", "lo-calhost"},
                {
                    "config",  new Dictionary<string, object>
                    {
                        {"total_ram_mib", 2048}
                    }
                }
            };
            var expectedJson = JsonConvert.SerializeObject(expectedObject);
            Assert.AreEqual(expectedJson, actualJson, string.Format("Expected {0}, but was {1}.", expectedJson, JsonConvert.SerializeObject(settingsModel)));
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
