// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.AgentHealth;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Helpers;
using NewRelic.Agent.Extensions.Logging;
using NewRelic.Agent.Core.SharedInterfaces;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.IO;
using NewRelic.Agent.Extensions.SystemExtensions.Collections.Generic;

namespace NewRelic.Agent.Core.Utilization
{
    public class VendorInfo
    {
        private const string ValidateMetadataRegex = @"^[a-zA-Z0-9-_. /]*$";
        private const string ContainerIdV1Regex = @".*cpu.*([0-9a-f]{64})";
        private const string ContainerIdV2Regex = ".*/docker/containers/([0-9a-f]{64})/.*";
        private const string AwsEcsMetadataV3EnvVar = "ECS_CONTAINER_METADATA_URI";
        private const string AwsEcsMetadataV4EnvVar = "ECS_CONTAINER_METADATA_URI_V4";

        private const string AwsName = @"aws";
        private const string AzureName = @"azure";
        private const string AzureFunctionAppName = @"azurefunction";
        private const string GcpName = @"gcp";
        private const string PcfName = @"pcf";
        private const string DockerName = @"docker";
        private const string KubernetesName = @"kubernetes";
        private const string EcsFargateName = @"ecs-fargate";

        private readonly string AwsTokenUri = @"http://169.254.169.254/latest/api/token";
        private readonly string AwsMetadataUri = @"http://169.254.169.254/latest/dynamic/instance-identity/document";
        private const string AwsTokenDurationHeader = "X-aws-ec2-metadata-token-ttl-seconds: 10";

        private readonly string AzureUri = @"http://169.254.169.254/metadata/instance/compute?api-version=2017-03-01";
        private const string AzureHeader = @"Metadata: true";

        private readonly string GcpUri = @"http://metadata.google.internal/computeMetadata/v1/instance/?recursive=true";
        private const string GcpHeader = @"Metadata-Flavor: Google";

        private const string PcfInstanceGuid = @"CF_INSTANCE_GUID";
        private const string PcfInstanceIp = @"CF_INSTANCE_IP";
        private const string PcfMemoryLimit = @"MEMORY_LIMIT";

        private readonly IConfiguration _configuration;
        private readonly IAgentHealthReporter _agentHealthReporter;
        private readonly IEnvironment _environment;
        private readonly VendorHttpApiRequestor _vendorHttpApiRequestor;

        private const string GetMethod = "GET";
        private const string PutMethod = "PUT";

        public VendorInfo(IConfiguration configuration, IAgentHealthReporter agentHealthReporter, IEnvironment environment, VendorHttpApiRequestor vendorHttpApiRequestor)
        {
            _configuration = configuration;
            _agentHealthReporter = agentHealthReporter;
            _environment = environment;
            _vendorHttpApiRequestor = vendorHttpApiRequestor;
        }

        public IDictionary<string, IVendorModel> GetVendors()
        {

            var vendors = new Dictionary<string, IVendorModel>();

            var vendorMethods = new List<Func<IVendorModel>>();

            if (_configuration.UtilizationDetectAws)
                vendorMethods.Add(GetAwsVendorInfo);
            if (_configuration.UtilizationDetectAzure)
                vendorMethods.Add(GetAzureVendorInfo);
            if (_configuration.UtilizationDetectGcp)
                vendorMethods.Add(GetGcpVendorInfo);
            if (_configuration.UtilizationDetectPcf)
                vendorMethods.Add(GetPcfVendorInfo);
            if (_configuration.UtilizationDetectAzureFunction)
                vendorMethods.Add(GetAzureFunctionVendorInfo);

            foreach (var vendorMethod in vendorMethods)
            {
                var vendorResult = vendorMethod();

                if (vendorResult != null)
                {
                    // We can break out once we've found a vendor as they are mutually exclusive.
                    vendors.Add(vendorResult.VendorName, vendorResult);
                    break;
                }
            }

            // If Docker info is set to be checked, it must be checked for all vendors.
            if (_configuration.UtilizationDetectDocker)
            {
                var dockerVendorInfo = GetDockerVendorInfo(new FileReaderWrapper(), IsLinux());
                if (dockerVendorInfo != null)
                {
                    vendors.Add(dockerVendorInfo.VendorName, dockerVendorInfo);
                }
            }

            if (_configuration.UtilizationDetectKubernetes)
            {
                var kubernetesInfo = GetKubernetesInfo();
                if (kubernetesInfo != null)
                {
                    vendors.Add(kubernetesInfo.VendorName, kubernetesInfo);
                }
            }

            return vendors;
        }

        public IVendorModel GetAzureFunctionVendorInfo()
        {
            if (!(_configuration.AzureFunctionModeDetected && _configuration.AzureFunctionModeEnabled))
                return null;

            var appName = _configuration.AzureFunctionResourceId;
            var cloudRegion = _configuration.AzureFunctionRegion;

            if (appName == null || cloudRegion == null)
            {
                return null;
            }

            return new AzureFunctionVendorModel(appName, cloudRegion);
        }

        private IVendorModel GetAwsVendorInfo()
        {
            var token = _vendorHttpApiRequestor.CallVendorApi(new Uri(AwsTokenUri), PutMethod, AwsName, new List<string> { AwsTokenDurationHeader });
            if (string.IsNullOrWhiteSpace(token))
            {
                return null;
            }

            var responseString = _vendorHttpApiRequestor.CallVendorApi(new Uri(AwsMetadataUri), GetMethod, AwsName, new List<string> { "X-aws-ec2-metadata-token: " + token });
            if (string.IsNullOrWhiteSpace(responseString))
            {
                return null;
            }

            return ParseAwsVendorInfo(responseString);
        }

        public IVendorModel ParseAwsVendorInfo(string json)
        {
            try
            {
                var jObject = JObject.Parse(json);

                var availabilityZoneToken = jObject.SelectToken("availabilityZone");
                var instanceIdToken = jObject.SelectToken("instanceId");
                var instanceTypeToken = jObject.SelectToken("instanceType");

                var availabilityZone = NormalizeAndValidateMetadata((string)availabilityZoneToken, "availabilityZone", AwsName);
                var instanceId = NormalizeAndValidateMetadata((string)instanceIdToken, "instanceId", AwsName);
                var instanceType = NormalizeAndValidateMetadata((string)instanceTypeToken, "instanceType", AwsName);

                if (availabilityZone == null || instanceId == null || instanceType == null)
                {
                    return null;
                }

                return new AwsVendorModel(availabilityZone, instanceId, instanceType);
            }
            catch
            {
                return null;
            }
        }

        private IVendorModel GetAzureVendorInfo()
        {
            var responseString = _vendorHttpApiRequestor.CallVendorApi(new Uri(AzureUri), GetMethod, AzureName, new List<string> { AzureHeader });
            if (responseString != null)
            {
                return ParseAzureVendorInfo(responseString);
            }

            return null;
        }

        public IVendorModel ParseAzureVendorInfo(string json)
        {
            try
            {
                var jObject = JObject.Parse(json);

                var locationToken = jObject.SelectToken("location");
                var nameToken = jObject.SelectToken("name");
                var vmIdToken = jObject.SelectToken("vmId");
                var vmSizeToken = jObject.SelectToken("vmSize");

                var location = NormalizeAndValidateMetadata((string)locationToken, "location", AzureName);
                var name = NormalizeAndValidateMetadata((string)nameToken, "name", AzureName);
                var vmId = NormalizeAndValidateMetadata((string)vmIdToken, "vmId", AzureName);
                var vmSize = NormalizeAndValidateMetadata((string)vmSizeToken, "vmSize", AzureName);

                if (location == null || name == null || vmId == null || vmSize == null)
                {
                    return null;
                }

                return new AzureVendorModel(location, name, vmId, vmSize);
            }
            catch
            {
                return null;
            }
        }

        private IVendorModel GetGcpVendorInfo()
        {
            var responseString = _vendorHttpApiRequestor.CallVendorApi(new Uri(GcpUri), GetMethod, GcpName, new List<string> { GcpHeader });
            if (responseString != null)
            {
                return ParseGcpVendorInfo(responseString);
            }

            return null;
        }

        public IVendorModel ParseGcpVendorInfo(string json)
        {
            try
            {
                var jObject = JObject.Parse(json);

                var idToken = jObject.SelectToken("id");
                var machineTypeToken = jObject.SelectToken("machineType");
                var nameToken = jObject.SelectToken("name");
                var zoneToken = jObject.SelectToken("zone");

                var id = NormalizeAndValidateMetadata((string)idToken, "id", GcpName);

                var machineTypeString = (string)machineTypeToken;
                var machineType = (machineTypeString != null) ? NormalizeAndValidateMetadata(machineTypeString.Split(StringSeparators.PathSeparator).Last(), "machineType", GcpName) : null;

                var name = NormalizeAndValidateMetadata((string)nameToken, "name", GcpName);

                var zoneTokenString = (string)zoneToken;
                var zone = (zoneTokenString != null) ? NormalizeAndValidateMetadata(zoneTokenString.Split(StringSeparators.PathSeparator).Last(), "zone", GcpName) : null;

                if (id == null || machineType == null || name == null || zone == null)
                {
                    return null;
                }

                return new GcpVendorModel(id, machineType, name, zone);
            }
            catch
            {
                return null;
            }
        }

        public IVendorModel GetPcfVendorInfo()
        {
            var instanceGuid = NormalizeAndValidateMetadata(GetProcessEnvironmentVariable(PcfInstanceGuid), "cf_instance_guid", PcfName);
            var instanceIp = NormalizeAndValidateMetadata(GetProcessEnvironmentVariable(PcfInstanceIp), "cf_instance_ip", PcfName);
            var memoryLimit = NormalizeAndValidateMetadata(GetProcessEnvironmentVariable(PcfMemoryLimit), "memory_limit", PcfName);

            if (instanceGuid == null || instanceIp == null || memoryLimit == null)
            {
                return null;
            }

            return new PcfVendorModel(instanceGuid, instanceIp, memoryLimit);
        }

        private string GetProcessEnvironmentVariable(string variableName)
        {
            try
            {
                var variableValue = _environment.GetEnvironmentVariable(variableName, EnvironmentVariableTarget.Process);
                return variableValue;
            }
            catch
            {
                return null;
            }
        }

        public IVendorModel GetDockerVendorInfo(IFileReaderWrapper fileReaderWrapper, bool isLinux)
        {
            IVendorModel vendorModel = null;
            if (isLinux)
            {
                try
                {
                    var fileContent = fileReaderWrapper.ReadAllText("/proc/self/mountinfo");
                    vendorModel = TryGetDockerCGroupV2(fileContent);
                    if (vendorModel == null)
                        Log.Finest("Found /proc/self/mountinfo but failed to parse Docker container id.");

                }
                catch (Exception ex)
                {
                    Log.Finest(ex, "Failed to parse Docker container id from /proc/self/mountinfo.");
                }

                if (vendorModel == null) // fall back to the v1 check if v2 wasn't successful
                {
                    try
                    {
                        var fileContent = fileReaderWrapper.ReadAllText("/proc/self/cgroup");
                        vendorModel = TryGetDockerCGroupV1(fileContent);
                        if (vendorModel == null)
                            Log.Finest("Found /proc/self/cgroup but failed to parse Docker container id.");
                    }
                    catch (Exception ex)
                    {
                        Log.Finest(ex, "Failed to parse Docker container id from /proc/self/cgroup.");
                    }
                }
            }

            if (vendorModel == null)
            {
                try
                {
                    var metadataUri = GetProcessEnvironmentVariable(AwsEcsMetadataV4EnvVar);
                    if (!string.IsNullOrWhiteSpace(metadataUri))
                    {
                        vendorModel = TryGetEcsFargateDockerId(metadataUri);
                        if (vendorModel == null)
                            Log.Finest($"Found {AwsEcsMetadataV4EnvVar} but failed to parse Docker container id.");
                    }
                }
                catch (Exception ex)
                {
                    Log.Finest(ex, $"Failed to parse Docker container id from {AwsEcsMetadataV4EnvVar}.");
                }
            }

            if (vendorModel == null)
            {
                try
                {
                    var metadataUri = GetProcessEnvironmentVariable(AwsEcsMetadataV3EnvVar);
                    if (!string.IsNullOrWhiteSpace(metadataUri))
                    {
                        vendorModel = TryGetEcsFargateDockerId(metadataUri);
                        if (vendorModel == null)
                            Log.Finest($"Found {AwsEcsMetadataV3EnvVar} but failed to parse Docker container id.");
                    }
                }
                catch (Exception ex)
                {
                    Log.Finest(ex, $"Failed to parse Docker container id from {AwsEcsMetadataV3EnvVar}.");
                }
            }

            return vendorModel;
        }

        private IVendorModel TryGetDockerCGroupV1(string fileContent)
        {
            string id;
            var matches = Regex.Matches(fileContent, ContainerIdV1Regex);
            if (TryGetIdFromRegexMatch(matches, out id))
            {
                return new DockerVendorModel(id);
            }
            return null;
        }

        private bool TryGetIdFromRegexMatch(MatchCollection matches, out string id)
        {
            id = null;
            if (matches.Count > 0)
            {
                var firstMatch = matches[0];
                if (firstMatch.Success && firstMatch.Groups.Count > 1 && firstMatch.Groups[1].Success)
                {
                    id = firstMatch.Groups[1].Value;
                    return true;
                }
            }
            return false;
        }

        private IVendorModel TryGetDockerCGroupV2(string fileContent)
        {
            string id = null;
            var matches = Regex.Matches(fileContent, ContainerIdV2Regex);
            if (matches.Count > 0)
            {
                var firstMatch = matches[0];
                if (firstMatch.Success && firstMatch.Groups.Count > 1 && firstMatch.Groups[1].Success)
                {
                    id = firstMatch.Groups[1].Value;
                }
            }

            return id == null ? null : new DockerVendorModel(id);
        }

        private IVendorModel TryGetEcsFargateDockerId(string metadataUri)
        {
            var responseJson = _vendorHttpApiRequestor.CallVendorApi(new Uri(metadataUri), GetMethod, EcsFargateName);
            var jObject = JObject.Parse(responseJson);
            var idToken = jObject.SelectToken("DockerId");
            var id = NormalizeAndValidateMetadata((string)idToken, "DockerId", EcsFargateName);
            return id == null ? null : new DockerVendorModel(id);
        }

        public IVendorModel GetKubernetesInfo()
        {
            var envVar = _environment.GetEnvironmentVariable("KUBERNETES_SERVICE_HOST");
            var kubernetesServiceHost = NormalizeAndValidateMetadata(envVar, "kubernetes_service_host", KubernetesName);
            return kubernetesServiceHost != null ? new KubernetesVendorModel(kubernetesServiceHost) : null;
        }

        public string NormalizeAndValidateMetadata(string metadataValue, string metadataField, string vendorName)
        {
            if (metadataValue == null)
                return null;

            var normalizedValue = NormalizeString(metadataValue);

            if (normalizedValue.IsEmpty())
                return null;

            if (!IsValidMetadata(normalizedValue))
            {
                Log.Info("Unable to validate {0} metadata for the {1} field.", vendorName, metadataField);

                switch (vendorName)
                {
                    case AwsName:
                        _agentHealthReporter.ReportAwsUtilizationError();
                        break;
                    case AzureName:
                        _agentHealthReporter.ReportAzureUtilizationError();
                        break;
                    case GcpName:
                        _agentHealthReporter.ReportGcpUtilizationError();
                        break;
                    case PcfName:
                        _agentHealthReporter.ReportPcfUtilizationError();
                        break;
                    case DockerName:
                        _agentHealthReporter.ReportBootIdError();
                        break;
                    case KubernetesName:
                        _agentHealthReporter.ReportKubernetesUtilizationError();
                        break;
                }

                return null;
            }

            return normalizedValue;
        }

        private string NormalizeString(string data)
        {
            return Clamper.ClampLength(data.Trim(), 255);
        }

        public bool IsValidMetadata(string data)
        {
            return Regex.IsMatch(data, ValidateMetadataRegex);
        }

        private static bool IsLinux()
        {
#if NETSTANDARD2_0
            return System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux);
#else
            return false; // No Linux on .NET Framework
#endif
        }
    }

    // needed for unit testing only
    public interface IFileReaderWrapper
    {
        string ReadAllText(string fileName);
    }

    public class FileReaderWrapper : IFileReaderWrapper
    {
        public string ReadAllText(string fileName)
        {
            return File.ReadAllText(fileName);
        }
    }
}
