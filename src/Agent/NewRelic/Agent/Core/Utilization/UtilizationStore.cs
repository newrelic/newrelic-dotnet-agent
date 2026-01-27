// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using NewRelic.Agent.Core.AgentHealth;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Core.SharedInterfaces;
using NewRelic.Agent.Extensions.Logging;

namespace NewRelic.Agent.Core.Utilization;

public class UtilizationStore
{
    private readonly ISystemInfo _systemInfo;

    private readonly IDnsStatic _dnsStatic;

    private readonly IConfiguration _configuration;

    private readonly IAgentHealthReporter _agentHealthReporter;
    private readonly IFileWrapper _fileWrapper;

    private const int MaxBootIdLength = 128;

    public UtilizationStore(ISystemInfo systemInfo, IDnsStatic dnsStatic, IConfiguration configuration, IAgentHealthReporter agentHealthReporter, IFileWrapper fileWrapper)
    {
        _systemInfo = systemInfo;
        _dnsStatic = dnsStatic;
        _configuration = configuration;
        _agentHealthReporter = agentHealthReporter;
        _fileWrapper = fileWrapper;
    }

    public UtilizationSettingsModel GetUtilizationSettings()
    {
        var totalMemory = _systemInfo.GetTotalPhysicalMemoryBytes();
        var logicalProcessors = _systemInfo.GetTotalLogicalProcessors();
        var hostname = _configuration.UtilizationHostName;
        var fullHostName = _configuration.UtilizationFullHostName;
        var ipAddress = _dnsStatic.GetIpAddresses();
        var vendors = GetVendorSettings();
        var bootIdResult = _systemInfo.GetBootId();

        if (!bootIdResult.IsValid)
        {
            Log.Warn("boot_id is not in expected format.");
            _agentHealthReporter.ReportBootIdError();
        }

        //if bootId is longer than 128 characters, truncate it to 128 characters.
        var bootId = Truncate(bootIdResult.BootId, MaxBootIdLength);
        return new UtilizationSettingsModel(logicalProcessors, totalMemory, hostname, fullHostName, ipAddress, bootId, vendors, GetUtilitizationConfig());
    }

    private string Truncate(string bootId, int maxLength)
    {
        return bootId?.Length > maxLength ? bootId.Substring(0, maxLength) : bootId;
    }

    public IDictionary<string, IVendorModel> GetVendorSettings()
    {
        var vendorInfo = new VendorInfo(_configuration, _agentHealthReporter, new SharedInterfaces.Environment(), new VendorHttpApiRequestor(), _fileWrapper);
        return vendorInfo.GetVendors();
    }

    private UtilitizationConfig GetUtilitizationConfig()
    {
        if (_configuration == null)
        {
            return null;
        }

        if (string.IsNullOrEmpty(_configuration.UtilizationBillingHost)
            && _configuration.UtilizationLogicalProcessors == null
            && _configuration.UtilizationTotalRamMib == null)
        {
            return null;
        }

        return new UtilitizationConfig(_configuration.UtilizationBillingHost, _configuration.UtilizationLogicalProcessors, _configuration.UtilizationTotalRamMib);
    }
}