// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.Collections.Generic;
using Microsoft.Extensions.Configuration;

namespace NewRelic.Agent.IntegrationTests.Shared
{
    public class IntegrationTestConfiguration
    {
        private string _configurationCategory { get; set; }

        public TestSettings DefaultSetting { get; set; } = new TestSettings();

        public Dictionary<string, TestSettings> TestSettingOverrides { get; set; } = new Dictionary<string, TestSettings>();

        public string this[string key]
        {
            get { return TestSettingOverrides[_configurationCategory].CustomSettings[key]; }
        }

        public string LicenseKey
        {
            get
            {
                if (TestSettingOverrides.TryGetValue(_configurationCategory, out var item) && !string.IsNullOrEmpty(item.LicenseKey))
                {
                    return item.LicenseKey;
                };

                return DefaultSetting.LicenseKey;
            }
        }

        public string CollectorUrl
        {
            get
            {
                return DefaultSetting.Collector;
            }
        }

        public string NewRelicAccountId
        {
            get
            {
                return DefaultSetting.NewRelicAccountId;
            }
        }

        public string TraceObserverUrl
        {
            get
            {
                if (TestSettingOverrides.TryGetValue(_configurationCategory, out var item) && !string.IsNullOrEmpty(item.TraceObserverUrl))
                {
                    return item.TraceObserverUrl;
                };

                return DefaultSetting.TraceObserverUrl;
            }
        }
        public string TraceObserverPort
        {
            get
            {
                if (TestSettingOverrides.TryGetValue(_configurationCategory, out var item) && !string.IsNullOrEmpty(item.TraceObserverPort))
                {
                    return item.TraceObserverPort;
                };

                return DefaultSetting.TraceObserverPort;
            }
        }

        private IntegrationTestConfiguration(string configurationCategory)
        {
            _configurationCategory = configurationCategory;
        }

        public static IntegrationTestConfiguration GetIntegrationTestConfiguration(string configurationCategory)
        {
            try
            {
                var configuration = new IntegrationTestConfiguration(configurationCategory);

                var iConfig = new ConfigurationBuilder()
                .AddUserSecrets<IntegrationTestConfiguration>()
                .Build();

                iConfig.GetSection(typeof(IntegrationTestConfiguration).Name)
                .Bind(configuration);

                return configuration;
            }
            catch
            {
                throw;
            }
        }
    }

    public class TestSettings
    {
        public string LicenseKey { get; set; }
        public string Collector { get; set; }
        public string NewRelicAccountId { get; set; }
        public string AwsTestRoleArn { get; set; }
        public string AwsAccessKeyId { get; set; }
        public string AwsSecretAccessKey { get; set; }
        public string AwsRegion { get; set; }
        public string AwsAccountNumber { get; set; }
        public string TraceObserverUrl { get; set; }
        public IDictionary<string, string> CustomSettings { get; set; }
        public string TraceObserverPort { get; set; } = "443";
    }

}
