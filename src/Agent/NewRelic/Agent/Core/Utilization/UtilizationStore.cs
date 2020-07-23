using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using JetBrains.Annotations;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.AgentHealth;
using NewRelic.Agent.Core.Logging;
using NewRelic.Agent.Core.Utilities;
using NewRelic.SystemInterfaces;

namespace NewRelic.Agent.Core.Utilization
{
    public class UtilizationStore
    {
        // AWS metadata accessor URIs
        private const string AwsVendorName = @"aws";
        private const string AwsIdUri = @"http://169.254.169.254/2008-02-01/meta-data/instance-id";
        private const string AwsTypeUri = @"http://169.254.169.254/2008-02-01/meta-data/instance-type";
        private const string AwsZoneUri = @"http://169.254.169.254/2008-02-01/meta-data/placement/availability-zone";

        [NotNull]
        private readonly ISystemInfo _systemInfo;

        [NotNull]
        private readonly IDnsStatic _dnsStatic;

        [CanBeNull]
        private readonly IConfiguration _configuration;

        [NotNull]
        private readonly IAgentHealthReporter _agentHealthReporter;

        private const int MaxBootIdLength = 128;

        public UtilizationStore([NotNull] ISystemInfo systemInfo, [NotNull] IDnsStatic dnsStatic, [CanBeNull] IConfiguration configuration, [NotNull] IAgentHealthReporter agentHealthReporter)
        {
            _systemInfo = systemInfo;
            _dnsStatic = dnsStatic;
            _configuration = configuration;
            _agentHealthReporter = agentHealthReporter;
        }

        [NotNull]
        public UtilizationSettingsModel GetUtilizationSettings()
        {
            var totalMemory = _systemInfo.GetTotalPhysicalMemoryBytes();
            var logicalProcessors = _systemInfo.GetTotalLogicalProcessors();
            var hostname = _dnsStatic.GetHostName();
            var vendors = GetVendorSettings();
            var bootIdResult = _systemInfo.GetBootId();


            if (!bootIdResult.IsValid)
            {
                Log.Warn("boot_id is not in expected format.");
                _agentHealthReporter.ReportBootIdError();
            }

            //if bootId is longer than 128 characters, truncate it to 128 characters.
            var bootId = Truncate(bootIdResult.BootId, MaxBootIdLength);
            return new UtilizationSettingsModel(logicalProcessors, totalMemory, hostname, bootId, vendors, GetUtilitizationConfig());
        }

        private string Truncate(string bootId, int maxLength)
        {
            return bootId?.Length > maxLength ? bootId.Substring(0, maxLength) : bootId;
        }

        [NotNull]
        public IEnumerable<IVendorModel> GetVendorSettings()
        {
            return new[] { GetAwsVendorInfo() }
                .Where(vendor => vendor != null);
        }

        [CanBeNull]
        public IVendorModel GetAwsVendorInfo()
        {
            var awsId = GetHttpResponseString(AwsIdUri, AwsVendorName);
            if (awsId == null)
                return null;

            return new AwsVendorModel(awsId, GetHttpResponseString(AwsTypeUri, AwsVendorName), GetHttpResponseString(AwsZoneUri, AwsVendorName));
        }

        [CanBeNull]
        private string GetHttpResponseString([NotNull] string uri, [NotNull] string vendorName)
        {
            try
            {
                var request = WebRequest.Create(uri);
                request.Timeout = (int)TimeSpan.FromSeconds(1).TotalMilliseconds;
                request.Method = "GET";
                using (var response = request.GetResponse() as HttpWebResponse)
                {
                    if (response == null)
                        return null;

                    var stream = response.GetResponseStream();
                    if (stream == null)
                        return null;

                    var reader = new StreamReader(stream);
                    return NormalizeString(reader.ReadToEnd());
                }
            }
            catch
            {
                return null;
            }
        }

        [NotNull]
        private string NormalizeString([NotNull] string data)
        {
            return Clamper.ClampLength(data.Trim(), 255);
        }

        [CanBeNull]
        private UtilitizationConfig GetUtilitizationConfig()
        {
            if (_configuration == null)
            {
                return null;
            }

            if (String.IsNullOrEmpty(_configuration.UtilizationBillingHost)
                && _configuration.UtilizationLogicalProcessors == null
                && _configuration.UtilizationTotalRamMib == null)
            {
                return null;
            }

            return new UtilitizationConfig(_configuration.UtilizationBillingHost, _configuration.UtilizationLogicalProcessors, _configuration.UtilizationTotalRamMib);
        }
    }
}
