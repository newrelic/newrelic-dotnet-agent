// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.AgentHealth;
using NewRelic.Agent.Core.SharedInterfaces;
using NUnit.Framework;
using Telerik.JustMock;


namespace NewRelic.Agent.Core.Utilization
{

    [TestFixture]
    public class VendorInfoTests
    {
        private IConfiguration _configuration;
        private IAgentHealthReporter _agentHealthReporter;
        private IEnvironment _environment;
        private VendorHttpApiRequestor _vendorHttpApiRequestor;

        private const string PcfInstanceGuid = @"CF_INSTANCE_GUID";
        private const string PcfInstanceIp = @"CF_INSTANCE_IP";
        private const string PcfMemoryLimit = @"MEMORY_LIMIT";
        private const string KubernetesServiceHost = @"KUBERNETES_SERVICE_HOST";
        private const string AwsEcsMetadataV3EnvVar = "ECS_CONTAINER_METADATA_URI";
        private const string AwsEcsMetadataV4EnvVar = "ECS_CONTAINER_METADATA_URI_V4";

        [SetUp]
        public void Setup()
        {
            _configuration = Mock.Create<IConfiguration>();
            _agentHealthReporter = Mock.Create<IAgentHealthReporter>();
            _environment = Mock.Create<IEnvironment>();
            _vendorHttpApiRequestor = Mock.Create<VendorHttpApiRequestor>();
        }

        [Test]
        public void GetVendors_Returns_Empty_Dictionary_When_Detect_True_And_Data_Unavailable()
        {
            Mock.Arrange(() => _configuration.UtilizationDetectAws).Returns(true);
            Mock.Arrange(() => _configuration.UtilizationDetectAzure).Returns(true);
            Mock.Arrange(() => _configuration.UtilizationDetectGcp).Returns(true);
            Mock.Arrange(() => _configuration.UtilizationDetectPcf).Returns(true);
            Mock.Arrange(() => _configuration.UtilizationDetectDocker).Returns(true);
            Mock.Arrange(() => _configuration.UtilizationDetectAzureFunction).Returns(true);

            var vendorInfo = new VendorInfo(_configuration, _agentHealthReporter, _environment, _vendorHttpApiRequestor);
            var vendors = vendorInfo.GetVendors();
            Assert.That(vendors.Any(), Is.False);
        }

        [Test]
        public void GetVendors_Returns_Empty_Dictionary_When_Detect_False()
        {
            var vendorInfo = new VendorInfo(_configuration, _agentHealthReporter, _environment, _vendorHttpApiRequestor);
            var vendors = vendorInfo.GetVendors();
            Assert.That(vendors.Any(), Is.False);
        }

        [TestCase("?", "location", "azure", null)]
        [TestCase("westus2", "location", "azure", "westus2")]
        [TestCase("<script>do something </script>", "zone", "gcp", null)]
        [TestCase("ddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd260", "zone", "aws", "ddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd")]
        [TestCase("10.96.0.1", "kubernetes_service_host", "kubernetes", "10.96.0.1")]
        public void GetVendors_NormalizeAndValidateMetadata(string metadataValue, string metadataField, string vendorName, string expectedResponse)
        {
            var vendorInfo = new VendorInfo(_configuration, _agentHealthReporter, _environment, _vendorHttpApiRequestor);
            var result = vendorInfo.NormalizeAndValidateMetadata(metadataValue, metadataField, vendorName);

            if (expectedResponse == null)
            {
                Assert.That(result, Is.Null);
            }
            else
            {
                Assert.That(result, Is.EqualTo(expectedResponse));
            }
        }

        [Test]
        public void GetVendors_ParseAwsVendorInfo_Complete()
        {
            var json = @"{
							""availabilityZone"" : ""us - east - 1d"",
							""instanceId"" : ""i-1234567890abcdef0"",
							""instanceType"" : ""t1.micro""
						}";

            var vendorInfo = new VendorInfo(_configuration, _agentHealthReporter, _environment, _vendorHttpApiRequestor);
            var model = (AwsVendorModel)vendorInfo.ParseAwsVendorInfo(json);

            Assert.That(model, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(model.InstanceId, Is.EqualTo("i-1234567890abcdef0"));
                Assert.That(model.InstanceType, Is.EqualTo("t1.micro"));
                Assert.That(model.AvailabilityZone, Is.EqualTo("us - east - 1d"));
            });
        }

        [Test]
        public void GetVendors_ParseAwsVendorInfo_MissingInvalidValues()
        {
            var json = @"{
							""instanceId"" : ""i-1234567890abcdef0"",
							""instanceType"" : ""t1.$micro""
						}";

            var vendorInfo = new VendorInfo(_configuration, _agentHealthReporter, _environment, _vendorHttpApiRequestor);
            var model = (AwsVendorModel)vendorInfo.ParseAwsVendorInfo(json);

            Assert.That(model, Is.Null);
        }

        [Test]
        public void GetVendors_ParseAwsVendorInfo_InvalidJson()
        {
            var json = @"{  I am not valid json. Deal with it.
							""availabilityZone"" : ""us - east - 1d"",
							""instanceId"" : ""i-1234567890abcdef0"",
							""instanceType"" : ""t1.micro""
						}";

            var vendorInfo = new VendorInfo(_configuration, _agentHealthReporter, _environment, _vendorHttpApiRequestor);
            var model = vendorInfo.ParseAwsVendorInfo(json);

            Assert.That(model, Is.Null);
        }

        [Test]
        public void GetVendors_ParseAzureVendorInfo_Complete()
        {
            var json = @"{
							  ""location"": ""CentralUS"",
							  ""name"": ""IMDSCanary"",
							  ""vmId"": ""5c08b38e-4d57-4c23-ac45-aca61037f084"",
							  ""vmSize"": ""Standard_DS2""
						}";

            var vendorInfo = new VendorInfo(_configuration, _agentHealthReporter, _environment, _vendorHttpApiRequestor);
            var model = (AzureVendorModel)vendorInfo.ParseAzureVendorInfo(json);

            Assert.That(model, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(model.Location, Is.EqualTo("CentralUS"));
                Assert.That(model.Name, Is.EqualTo("IMDSCanary"));
                Assert.That(model.VmId, Is.EqualTo("5c08b38e-4d57-4c23-ac45-aca61037f084"));
                Assert.That(model.VmSize, Is.EqualTo("Standard_DS2"));
            });
        }

        [Test]
        public void GetVendors_ParseAzureVendorInfo_MissingInvalidValues()
        {
            var json = @"{
							  ""location"": ""CentralUS"",
							  ""name"": ""IMDS:Canary"",
							  ""vmId"": ""5c08b38e-4d57-4c23-ac45-aca61037f084""
						}";

            var vendorInfo = new VendorInfo(_configuration, _agentHealthReporter, _environment, _vendorHttpApiRequestor);
            var model = (AzureVendorModel)vendorInfo.ParseAzureVendorInfo(json);

            Assert.That(model, Is.Null);
        }

        [Test]
        public void GetVendors_ParseAzureVendorInfo_InvalidJson()
        {
            var json = @"{
							  I am not valid json. Deal with it.
							  ""location"": ""CentralUS"",
							  ""name"": ""IMDSCanary"",
							  ""vmId"": ""5c08b38e-4d57-4c23-ac45-aca61037f084"",
							  ""vmSize"": ""Standard_DS2""
						}";

            var vendorInfo = new VendorInfo(_configuration, _agentHealthReporter, _environment, _vendorHttpApiRequestor);
            var model = (AzureVendorModel)vendorInfo.ParseAzureVendorInfo(json);

            Assert.That(model, Is.Null);
        }

        [Test]
        public void GetVendors_ParseGcpVendorInfo_Complete()
        {
            var json = @"{
							""id"": 3161347020215157000,
							""machineType"": ""projects / 492690098729 / machineTypes / custom - 1 - 1024"",
							""name"": ""aef-default-20170501t160547-7gh8"",
							""zone"": ""projects/492690098729/zones/us-central1-c""
						}";

            var vendorInfo = new VendorInfo(_configuration, _agentHealthReporter, _environment, _vendorHttpApiRequestor);
            var model = (GcpVendorModel)vendorInfo.ParseGcpVendorInfo(json);

            Assert.That(model, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(model.Id, Is.EqualTo("3161347020215157000"));
                Assert.That(model.MachineType, Is.EqualTo("custom - 1 - 1024"));
                Assert.That(model.Name, Is.EqualTo("aef-default-20170501t160547-7gh8"));
                Assert.That(model.Zone, Is.EqualTo("us-central1-c"));
            });
        }

        [Test]
        public void GetVendors_ParseGcpVendorInfo_MissingInvalidValues()
        {
            var json = @"{
							""id"": 3161347020215157000,
							""machineType"": ""projects / 492690098729 / machineTypes / custom - 1 - 1024"",
							""name"": ""aef-default-20170501t160547-7gh8?""
						}";

            var vendorInfo = new VendorInfo(_configuration, _agentHealthReporter, _environment, _vendorHttpApiRequestor);
            var model = (GcpVendorModel)vendorInfo.ParseGcpVendorInfo(json);

            Assert.That(model, Is.Null);
        }

        [Test]
        public void GetVendors_ParseGcpVendorInfo_InvalidJson()
        {
            var json = @"{
							""id"": 3161347020215157000,
							""machineType"": ""projects / 492690098729 / machineTypes / custom - 1 - 1024"",
							""name"": ""aef-default-20170501t160547-7gh8""
							I'm not valid json. Deal with it.
						}";

            var vendorInfo = new VendorInfo(_configuration, _agentHealthReporter, _environment, _vendorHttpApiRequestor);
            var model = (GcpVendorModel)vendorInfo.ParseGcpVendorInfo(json);

            Assert.That(model, Is.Null);
        }

        [Test]
        public void GetVendors_GetPcfVendorInfo_Complete()
        {
            SetEnvironmentVariable(PcfInstanceGuid, "b977d090-83db-4bdb-793a-bb77", EnvironmentVariableTarget.Process);
            SetEnvironmentVariable(PcfInstanceIp, "10.10.147.130", EnvironmentVariableTarget.Process);
            SetEnvironmentVariable(PcfMemoryLimit, "1024m", EnvironmentVariableTarget.Process);

            var vendorInfo = new VendorInfo(_configuration, _agentHealthReporter, _environment, _vendorHttpApiRequestor);
            var model = (PcfVendorModel)vendorInfo.GetPcfVendorInfo();

            Assert.That(model, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(model.CfInstanceGuid, Is.EqualTo("b977d090-83db-4bdb-793a-bb77"));
                Assert.That(model.CfInstanceIp, Is.EqualTo("10.10.147.130"));
                Assert.That(model.MemoryLimit, Is.EqualTo("1024m"));
            });
        }

        [Test]
        public void GetVendors_GetPcfVendorInfo_None()
        {
            SetEnvironmentVariable(PcfInstanceGuid, null, EnvironmentVariableTarget.Process);
            SetEnvironmentVariable(PcfInstanceIp, null, EnvironmentVariableTarget.Process);
            SetEnvironmentVariable(PcfMemoryLimit, null, EnvironmentVariableTarget.Process);

            var vendorInfo = new VendorInfo(_configuration, _agentHealthReporter, _environment, _vendorHttpApiRequestor);
            var model = (PcfVendorModel)vendorInfo.GetPcfVendorInfo();

            Assert.That(model, Is.Null);
        }

        [Test]
        public void GetVendors_GetKubernetesVendorInfo_Complete()
        {
            var serviceHost = "10.96.0.1";
            SetEnvironmentVariable(KubernetesServiceHost, serviceHost, EnvironmentVariableTarget.Process);

            var vendorInfo = new VendorInfo(_configuration, _agentHealthReporter, _environment, _vendorHttpApiRequestor);
            var model = (KubernetesVendorModel)vendorInfo.GetKubernetesInfo();

            Assert.That(model, Is.Not.Null);
            Assert.That(model.KubernetesServiceHost, Is.EqualTo(serviceHost));
        }

        [Test]
        public void GetVendors_GetKubernetesVendorInfo_None()
        {
            SetEnvironmentVariable(KubernetesServiceHost, null, EnvironmentVariableTarget.Process);

            var vendorInfo = new VendorInfo(_configuration, _agentHealthReporter, _environment, _vendorHttpApiRequestor);
            var model = (KubernetesVendorModel)vendorInfo.GetKubernetesInfo();

            Assert.That(model, Is.Null);
        }

#if NET
        [Test]
        public void GetVendors_GetDockerVendorInfo_ParsesV2()
        {
            var vendorInfo = new VendorInfo(_configuration, _agentHealthReporter, _environment, _vendorHttpApiRequestor);
            var mockFileReaderWrapper = Mock.Create<IFileReaderWrapper>();
            Mock.Arrange(() => mockFileReaderWrapper.ReadAllText("/proc/self/mountinfo")).Returns(@"
1425 1301 0:290 / / rw,relatime master:314 - overlay overlay rw,lowerdir=/var/lib/docker/overlay2/l/SEESBOIUB4X3HZXQDX5TSEQ7BN:/var/lib/docker/overlay2/l/MOPJN3KMFAIZGI5ENZ4O34OONV:/var/lib/docker/overlay2/l/HDHJSGZM5PTRBYHW5EAYHS7XRU:/var/lib/docker/overlay2/l/DPNQ4BZTYI2XJTICBFBZQ3LYGY:/var/lib/docker/overlay2/l/WHFN2B5YEUTYPT77F26T57WB5I:/var/lib/docker/overlay2/l/P7VISFMMKEWRYA7L34PW2O2J54:/var/lib/docker/overlay2/l/ZWNBERDCDMC6LTZHJ4L64AC5LD:/var/lib/docker/overlay2/l/UGWQJ4NGWITVZZNEXAK7ZHDQDD:/var/lib/docker/overlay2/l/IZ5XCLZYFBF7BC4XULL7IJWT3Q:/var/lib/docker/overlay2/l/EGK3Y3BMJAVWDQZLM4DFYAZQNJ:/var/lib/docker/overlay2/l/LNHVYS3UDT2S2TTN2TF3JVSHFH,upperdir=/var/lib/docker/overlay2/14399ff93af039f15ee6a9633110eaf5ac552802c589e7c5595e32adfb635d39/diff,workdir=/var/lib/docker/overlay2/14399ff93af039f15ee6a9633110eaf5ac552802c589e7c5595e32adfb635d39/work
1426 1425 0:293 / /proc rw,nosuid,nodev,noexec,relatime - proc proc rw
1427 1425 0:294 / /dev rw,nosuid - tmpfs tmpfs rw,size=65536k,mode=755
1428 1427 0:295 / /dev/pts rw,nosuid,noexec,relatime - devpts devpts rw,gid=5,mode=620,ptmxmode=666
1429 1425 0:296 / /sys ro,nosuid,nodev,noexec,relatime - sysfs sysfs ro
1453 1429 0:297 / /sys/fs/cgroup rw,nosuid,nodev,noexec,relatime - tmpfs tmpfs rw,mode=755
1454 1453 0:56 /docker/adf04870aa0a9f01fb712e283765ee5d7c7b1c1c0ad8ebfdea20a8bb3ae382fb /sys/fs/cgroup/cpuset ro,nosuid,nodev,noexec,relatime master:75 - cgroup cpuset rw,cpuset
1455 1453 0:57 /docker/adf04870aa0a9f01fb712e283765ee5d7c7b1c1c0ad8ebfdea20a8bb3ae382fb /sys/fs/cgroup/cpu ro,nosuid,nodev,noexec,relatime master:76 - cgroup cpu rw,cpu
1456 1453 0:58 /docker/adf04870aa0a9f01fb712e283765ee5d7c7b1c1c0ad8ebfdea20a8bb3ae382fb /sys/fs/cgroup/cpuacct ro,nosuid,nodev,noexec,relatime master:77 - cgroup cpuacct rw,cpuacct
1457 1453 0:59 /docker/adf04870aa0a9f01fb712e283765ee5d7c7b1c1c0ad8ebfdea20a8bb3ae382fb /sys/fs/cgroup/blkio ro,nosuid,nodev,noexec,relatime master:78 - cgroup blkio rw,blkio
1458 1453 0:60 /docker/adf04870aa0a9f01fb712e283765ee5d7c7b1c1c0ad8ebfdea20a8bb3ae382fb /sys/fs/cgroup/memory ro,nosuid,nodev,noexec,relatime master:79 - cgroup memory rw,memory
1459 1453 0:61 /docker/adf04870aa0a9f01fb712e283765ee5d7c7b1c1c0ad8ebfdea20a8bb3ae382fb /sys/fs/cgroup/devices ro,nosuid,nodev,noexec,relatime master:80 - cgroup devices rw,devices
1460 1453 0:62 /docker/adf04870aa0a9f01fb712e283765ee5d7c7b1c1c0ad8ebfdea20a8bb3ae382fb /sys/fs/cgroup/freezer ro,nosuid,nodev,noexec,relatime master:81 - cgroup freezer rw,freezer
1461 1453 0:63 /docker/adf04870aa0a9f01fb712e283765ee5d7c7b1c1c0ad8ebfdea20a8bb3ae382fb /sys/fs/cgroup/net_cls ro,nosuid,nodev,noexec,relatime master:82 - cgroup net_cls rw,net_cls
1462 1453 0:64 /docker/adf04870aa0a9f01fb712e283765ee5d7c7b1c1c0ad8ebfdea20a8bb3ae382fb /sys/fs/cgroup/perf_event ro,nosuid,nodev,noexec,relatime master:83 - cgroup perf_event rw,perf_event
1463 1453 0:65 /docker/adf04870aa0a9f01fb712e283765ee5d7c7b1c1c0ad8ebfdea20a8bb3ae382fb /sys/fs/cgroup/net_prio ro,nosuid,nodev,noexec,relatime master:84 - cgroup net_prio rw,net_prio
1464 1453 0:66 /docker/adf04870aa0a9f01fb712e283765ee5d7c7b1c1c0ad8ebfdea20a8bb3ae382fb /sys/fs/cgroup/hugetlb ro,nosuid,nodev,noexec,relatime master:85 - cgroup hugetlb rw,hugetlb
1465 1453 0:67 /docker/adf04870aa0a9f01fb712e283765ee5d7c7b1c1c0ad8ebfdea20a8bb3ae382fb /sys/fs/cgroup/pids ro,nosuid,nodev,noexec,relatime master:86 - cgroup pids rw,pids
1466 1453 0:68 /docker/adf04870aa0a9f01fb712e283765ee5d7c7b1c1c0ad8ebfdea20a8bb3ae382fb /sys/fs/cgroup/rdma ro,nosuid,nodev,noexec,relatime master:87 - cgroup rdma rw,rdma
1467 1453 0:69 /docker/adf04870aa0a9f01fb712e283765ee5d7c7b1c1c0ad8ebfdea20a8bb3ae382fb /sys/fs/cgroup/misc ro,nosuid,nodev,noexec,relatime master:88 - cgroup misc rw,misc
1468 1453 0:132 /docker/adf04870aa0a9f01fb712e283765ee5d7c7b1c1c0ad8ebfdea20a8bb3ae382fb /sys/fs/cgroup/systemd ro,nosuid,nodev,noexec,relatime master:89 - cgroup cgroup rw,name=systemd
1469 1427 0:292 / /dev/mqueue rw,nosuid,nodev,noexec,relatime - mqueue mqueue rw
1470 1427 0:298 / /dev/shm rw,nosuid,nodev,noexec,relatime - tmpfs shm rw,size=65536k
1471 1425 8:64 /data/docker/containers/adf04870aa0a9f01fb712e283765ee5d7c7b1c1c0ad8ebfdea20a8bb3ae382fb/resolv.conf /etc/resolv.conf rw,relatime - ext4 /dev/sde rw,discard,errors=remount-ro,data=ordered
1472 1425 8:64 /data/docker/containers/adf04870aa0a9f01fb712e283765ee5d7c7b1c1c0ad8ebfdea20a8bb3ae382fb/hostname /etc/hostname rw,relatime - ext4 /dev/sde rw,discard,errors=remount-ro,data=ordered
1473 1425 8:64 /data/docker/containers/adf04870aa0a9f01fb712e283765ee5d7c7b1c1c0ad8ebfdea20a8bb3ae382fb/hosts /etc/hosts rw,relatime - ext4 /dev/sde rw,discard,errors=remount-ro,data=ordered
1312 1426 0:293 /bus /proc/bus ro,nosuid,nodev,noexec,relatime - proc proc rw
1313 1426 0:293 /fs /proc/fs ro,nosuid,nodev,noexec,relatime - proc proc rw
1314 1426 0:293 /irq /proc/irq ro,nosuid,nodev,noexec,relatime - proc proc rw
1315 1426 0:293 /sys /proc/sys ro,nosuid,nodev,noexec,relatime - proc proc rw
1316 1426 0:299 / /proc/acpi ro,relatime - tmpfs tmpfs ro
1339 1426 0:294 /null /proc/kcore rw,nosuid - tmpfs tmpfs rw,size=65536k,mode=755
1340 1426 0:294 /null /proc/keys rw,nosuid - tmpfs tmpfs rw,size=65536k,mode=755
1341 1426 0:294 /null /proc/timer_list rw,nosuid - tmpfs tmpfs rw,size=65536k,mode=755
1342 1429 0:300 / /sys/firmware ro,relatime - tmpfs tmpfs ro
");

            var model = (DockerVendorModel)vendorInfo.GetDockerVendorInfo(mockFileReaderWrapper, true);
            Assert.That(model, Is.Not.Null);
            Assert.That(model.Id, Is.EqualTo("adf04870aa0a9f01fb712e283765ee5d7c7b1c1c0ad8ebfdea20a8bb3ae382fb"));
        }

        [Test]
        public void GetVendors_GetDockerVendorInfo_ParsesV1_IfV2LookupFailsToParseFile()
        {
            var vendorInfo = new VendorInfo(_configuration, _agentHealthReporter, _environment, _vendorHttpApiRequestor);
            var mockFileReaderWrapper = Mock.Create<IFileReaderWrapper>();
            Mock.Arrange(() => mockFileReaderWrapper.ReadAllText("/proc/self/mountinfo")).Returns("foo bar baz");
            Mock.Arrange(() => mockFileReaderWrapper.ReadAllText("/proc/self/cgroup")).Returns(@"
15:name=systemd:/docker/b9d734e13dc5f508571d975edade94a05dfc637e73a83e11077a39bc11681043
14:misc:/docker/b9d734e13dc5f508571d975edade94a05dfc637e73a83e11077a39bc11681043
13:rdma:/docker/b9d734e13dc5f508571d975edade94a05dfc637e73a83e11077a39bc11681043
12:pids:/docker/b9d734e13dc5f508571d975edade94a05dfc637e73a83e11077a39bc11681043
11:hugetlb:/docker/b9d734e13dc5f508571d975edade94a05dfc637e73a83e11077a39bc11681043
10:net_prio:/docker/b9d734e13dc5f508571d975edade94a05dfc637e73a83e11077a39bc11681043
9:perf_event:/docker/b9d734e13dc5f508571d975edade94a05dfc637e73a83e11077a39bc11681043
8:net_cls:/docker/b9d734e13dc5f508571d975edade94a05dfc637e73a83e11077a39bc11681043
7:freezer:/docker/b9d734e13dc5f508571d975edade94a05dfc637e73a83e11077a39bc11681043
6:devices:/docker/b9d734e13dc5f508571d975edade94a05dfc637e73a83e11077a39bc11681043
5:memory:/docker/b9d734e13dc5f508571d975edade94a05dfc637e73a83e11077a39bc11681043
4:blkio:/docker/b9d734e13dc5f508571d975edade94a05dfc637e73a83e11077a39bc11681043
3:cpuacct:/docker/b9d734e13dc5f508571d975edade94a05dfc637e73a83e11077a39bc11681043
2:cpu:/docker/b9d734e13dc5f508571d975edade94a05dfc637e73a83e11077a39bc11681043
1:cpuset:/docker/b9d734e13dc5f508571d975edade94a05dfc637e73a83e11077a39bc11681043
0::/docker/b9d734e13dc5f508571d975edade94a05dfc637e73a83e11077a39bc11681043");

            var model = (DockerVendorModel)vendorInfo.GetDockerVendorInfo(mockFileReaderWrapper, true);
            Assert.That(model, Is.Not.Null);
            Assert.That(model.Id, Is.EqualTo("b9d734e13dc5f508571d975edade94a05dfc637e73a83e11077a39bc11681043"));
        }

        // See https://new-relic.atlassian.net/browse/NR-221128 and https://new-relic.atlassian.net/browse/NR-230908
        // The sample files below are from a customer issue where the cgroup file was not being parsed correctly
        [Test]
        public void GetVendors_GetDockerVendorInfo_ParsesV1_ForCustomerIssue()
        {
            var vendorInfo = new VendorInfo(_configuration, _agentHealthReporter, _environment, _vendorHttpApiRequestor);
            var mockFileReaderWrapper = Mock.Create<IFileReaderWrapper>();
            Mock.Arrange(() => mockFileReaderWrapper.ReadAllText("/proc/self/mountinfo")).Returns(@"
14940 3711 0:1357 / / rw,relatime master:1603 - overlay overlay rw,lowerdir=/var/lib/containerd/io.containerd.snapshotter.v1.overlayfs/snapshots/40241/fs:/var/lib/containerd/io.containerd.snapshotter.v1.overlayfs/snapshots/40240/fs:/var/lib/containerd/io.containerd.snapshotter.v1.overlayfs/snapshots/40239/fs:/var/lib/containerd/io.containerd.snapshotter.v1.overlayfs/snapshots/40238/fs:/var/lib/containerd/io.containerd.snapshotter.v1.overlayfs/snapshots/40237/fs:/var/lib/containerd/io.containerd.snapshotter.v1.overlayfs/snapshots/40236/fs:/var/lib/containerd/io.containerd.snapshotter.v1.overlayfs/snapshots/40235/fs:/var/lib/containerd/io.containerd.snapshotter.v1.overlayfs/snapshots/4615/fs:/var/lib/containerd/io.containerd.snapshotter.v1.overlayfs/snapshots/4614/fs:/var/lib/containerd/io.containerd.snapshotter.v1.overlayfs/snapshots/4613/fs:/var/lib/containerd/io.containerd.snapshotter.v1.overlayfs/snapshots/4612/fs:/var/lib/containerd/io.containerd.snapshotter.v1.overlayfs/snapshots/4611/fs,upperdir=/var/lib/containerd/io.containerd.snapshotter.v1.overlayfs/snapshots/40242/fs,workdir=/var/lib/containerd/io.containerd.snapshotter.v1.overlayfs/snapshots/40242/work
14941 14940 0:1373 / / proc rw, nosuid, nodev, noexec, relatime - proc proc rw
14942 14940 0:1882 / / dev rw, nosuid - tmpfs tmpfs rw, size = 65536k, mode = 755
14943 14942 0:1883 / / dev / pts rw, nosuid, noexec, relatime - devpts devpts rw, gid = 5, mode = 620, ptmxmode = 666
14965 14942 0:1003 / / dev / mqueue rw, nosuid, nodev, noexec, relatime - mqueue mqueue rw
14966 14940 0:1140 / / sys ro, nosuid, nodev, noexec, relatime - sysfs sysfs ro
14967 14966 0:1953 / / sys / fs / cgroup rw, nosuid, nodev, noexec, relatime - tmpfs tmpfs rw, mode = 755
14968 14967 0:25 / kubepods.slice / kubepods - burstable.slice / kubepods - burstable - pod04f9c4b4_5e71_4a0a_aa3a_f62f089e3f73.slice / cri - containerd - b10c13eeeea82c495c9e2fbb07ab448024715fdd55218e22cce6cd815c84bd58.scope / sys / fs / cgroup / systemd ro, nosuid, nodev, noexec, relatime master: 9 - cgroup cgroup rw, xattr, release_agent =/ usr / lib / systemd / systemd - cgroups - agent, name = systemd
14969 14967 0:27 / kubepods.slice / kubepods - burstable.slice / kubepods - burstable - pod04f9c4b4_5e71_4a0a_aa3a_f62f089e3f73.slice / cri - containerd - b10c13eeeea82c495c9e2fbb07ab448024715fdd55218e22cce6cd815c84bd58.scope / sys / fs / cgroup / memory ro, nosuid, nodev, noexec, relatime master: 10 - cgroup cgroup rw, memory
14970 14967 0:28 / kubepods.slice / kubepods - burstable.slice / kubepods - burstable - pod04f9c4b4_5e71_4a0a_aa3a_f62f089e3f73.slice / cri - containerd - b10c13eeeea82c495c9e2fbb07ab448024715fdd55218e22cce6cd815c84bd58.scope / sys / fs / cgroup / devices ro, nosuid, nodev, noexec, relatime master: 11 - cgroup cgroup rw, devices
15019 14967 0:29 / kubepods.slice / kubepods - burstable.slice / kubepods - burstable - pod04f9c4b4_5e71_4a0a_aa3a_f62f089e3f73.slice / cri - containerd - b10c13eeeea82c495c9e2fbb07ab448024715fdd55218e22cce6cd815c84bd58.scope / sys / fs / cgroup / net_cls, net_prio ro, nosuid, nodev, noexec, relatime master: 12 - cgroup cgroup rw, net_cls, net_prio
15020 14967 0:30 / kubepods.slice / kubepods - burstable.slice / kubepods - burstable - pod04f9c4b4_5e71_4a0a_aa3a_f62f089e3f73.slice / cri - containerd - b10c13eeeea82c495c9e2fbb07ab448024715fdd55218e22cce6cd815c84bd58.scope / sys / fs / cgroup / pids ro, nosuid, nodev, noexec, relatime master: 13 - cgroup cgroup rw, pids
15021 14967 0:31 / kubepods.slice / kubepods - burstable.slice / kubepods - burstable - pod04f9c4b4_5e71_4a0a_aa3a_f62f089e3f73.slice / cri - containerd - b10c13eeeea82c495c9e2fbb07ab448024715fdd55218e22cce6cd815c84bd58.scope / sys / fs / cgroup / cpu, cpuacct ro, nosuid, nodev, noexec, relatime master: 14 - cgroup cgroup rw, cpu, cpuacct
15022 14967 0:32 / kubepods.slice / kubepods - burstable.slice / kubepods - burstable - pod04f9c4b4_5e71_4a0a_aa3a_f62f089e3f73.slice / cri - containerd - b10c13eeeea82c495c9e2fbb07ab448024715fdd55218e22cce6cd815c84bd58.scope / sys / fs / cgroup / perf_event ro, nosuid, nodev, noexec, relatime master: 15 - cgroup cgroup rw, perf_event
15023 14967 0:33 / kubepods.slice / kubepods - burstable.slice / kubepods - burstable - pod04f9c4b4_5e71_4a0a_aa3a_f62f089e3f73.slice / cri - containerd - b10c13eeeea82c495c9e2fbb07ab448024715fdd55218e22cce6cd815c84bd58.scope / sys / fs / cgroup / freezer ro, nosuid, nodev, noexec, relatime master: 16 - cgroup cgroup rw, freezer
15024 14967 0:34 / kubepods.slice / kubepods - burstable.slice / kubepods - burstable - pod04f9c4b4_5e71_4a0a_aa3a_f62f089e3f73.slice / cri - containerd - b10c13eeeea82c495c9e2fbb07ab448024715fdd55218e22cce6cd815c84bd58.scope / sys / fs / cgroup / cpuset ro, nosuid, nodev, noexec, relatime master: 17 - cgroup cgroup rw, cpuset
15025 14967 0:35 / kubepods.slice / kubepods - burstable.slice / kubepods - burstable - pod04f9c4b4_5e71_4a0a_aa3a_f62f089e3f73.slice / cri - containerd - b10c13eeeea82c495c9e2fbb07ab448024715fdd55218e22cce6cd815c84bd58.scope / sys / fs / cgroup / blkio ro, nosuid, nodev, noexec, relatime master: 18 - cgroup cgroup rw, blkio
15026 14967 0:36 / kubepods.slice / kubepods - burstable.slice / kubepods - burstable - pod04f9c4b4_5e71_4a0a_aa3a_f62f089e3f73.slice / cri - containerd - b10c13eeeea82c495c9e2fbb07ab448024715fdd55218e22cce6cd815c84bd58.scope / sys / fs / cgroup / hugetlb ro, nosuid, nodev, noexec, relatime master: 19 - cgroup cgroup rw, hugetlb
15027 14940 259:1 / var / lib / kubelet / pods / 04f9c4b4 - 5e71 - 4a0a - aa3a - f62f089e3f73 / etc - hosts / etc / hosts rw, noatime - xfs / dev / nvme0n1p1 rw, attr2, inode64, logbufs = 8, logbsize = 32k, noquota
15028 14942 259:1 / var / lib / kubelet / pods / 04f9c4b4 - 5e71 - 4a0a - aa3a - f62f089e3f73 / containers / bank - statement - data - extractor - v2 - webapi - test / 1bd80981 / dev / termination - log rw, noatime - xfs / dev / nvme0n1p1 rw, attr2, inode64, logbufs = 8, logbsize = 32k, noquota
15029 14940 259:1 / var / lib / containerd / io.containerd.grpc.v1.cri / sandboxes / 18845a93b0b73d68bd8bab4d75e3f109f6c387c073918ca4558eea4af96f29c6 / hostname / etc / hostname rw, noatime - xfs / dev / nvme0n1p1 rw, attr2, inode64, logbufs = 8, logbsize = 32k, noquota
15030 14940 259:1 / var / lib / containerd / io.containerd.grpc.v1.cri / sandboxes / 18845a93b0b73d68bd8bab4d75e3f109f6c387c073918ca4558eea4af96f29c6 / resolv.conf / etc / resolv.conf rw, noatime - xfs / dev / nvme0n1p1 rw, attr2, inode64, logbufs = 8, logbsize = 32k, noquota
15031 14942 0:160 / / dev / shm rw, nosuid, nodev, noexec, relatime - tmpfs shm rw, size = 65536k
15032 14940 0:156 / / run / secrets / kubernetes.io / serviceaccount ro, relatime - tmpfs tmpfs rw, size = 1048576k
15033 14940 0:152 / / run / secrets / eks.amazonaws.com / serviceaccount ro, relatime - tmpfs tmpfs rw, size = 1048576k
3715 14941 0:1373 / bus / proc / bus ro, nosuid, nodev, noexec, relatime - proc proc rw
3862 14941 0:1373 / fs / proc / fs ro, nosuid, nodev, noexec, relatime - proc proc rw
3863 14941 0:1373 / irq / proc / irq ro, nosuid, nodev, noexec, relatime - proc proc rw
3864 14941 0:1373 / sys / proc / sys ro, nosuid, nodev, noexec, relatime - proc proc rw
3866 14941 0:1373 / sysrq - trigger / proc / sysrq - trigger ro, nosuid, nodev, noexec, relatime - proc proc rw
3943 14941 0:1954 / / proc / acpi ro, relatime - tmpfs tmpfs ro
4016 14941 0:1882 / null / proc / kcore rw, nosuid - tmpfs tmpfs rw, size = 65536k, mode = 755
4028 14941 0:1882 / null / proc / keys rw, nosuid - tmpfs tmpfs rw, size = 65536k, mode = 755
4030 14941 0:1882 / null / proc / latency_stats rw, nosuid - tmpfs tmpfs rw, size = 65536k, mode = 755
4031 14941 0:1882 / null / proc / timer_list rw, nosuid - tmpfs tmpfs rw, size = 65536k, mode = 755
4032 14941 0:1882 / null / proc / sched_debug rw, nosuid - tmpfs tmpfs rw, size = 65536k, mode = 755
4033 14966 0:1955 / / sys / firmware ro, relatime - tmpfs tmpfs ro");

            Mock.Arrange(() => mockFileReaderWrapper.ReadAllText("/proc/self/cgroup")).Returns(@"
11:hugetlb:/kubepods.slice/kubepods-burstable.slice/kubepods-burstable-pod04f9c4b4_5e71_4a0a_aa3a_f62f089e3f73.slice/cri-containerd-b10c13eeeea82c495c9e2fbb07ab448024715fdd55218e22cce6cd815c84bd58.scope
10:blkio:/kubepods.slice/kubepods-burstable.slice/kubepods-burstable-pod04f9c4b4_5e71_4a0a_aa3a_f62f089e3f73.slice/cri-containerd-b10c13eeeea82c495c9e2fbb07ab448024715fdd55218e22cce6cd815c84bd58.scope
9:cpuset:/kubepods.slice/kubepods-burstable.slice/kubepods-burstable-pod04f9c4b4_5e71_4a0a_aa3a_f62f089e3f73.slice/cri-containerd-b10c13eeeea82c495c9e2fbb07ab448024715fdd55218e22cce6cd815c84bd58.scope
8:freezer:/kubepods.slice/kubepods-burstable.slice/kubepods-burstable-pod04f9c4b4_5e71_4a0a_aa3a_f62f089e3f73.slice/cri-containerd-b10c13eeeea82c495c9e2fbb07ab448024715fdd55218e22cce6cd815c84bd58.scope
7:perf_event:/kubepods.slice/kubepods-burstable.slice/kubepods-burstable-pod04f9c4b4_5e71_4a0a_aa3a_f62f089e3f73.slice/cri-containerd-b10c13eeeea82c495c9e2fbb07ab448024715fdd55218e22cce6cd815c84bd58.scope
6:cpu,cpuacct:/kubepods.slice/kubepods-burstable.slice/kubepods-burstable-pod04f9c4b4_5e71_4a0a_aa3a_f62f089e3f73.slice/cri-containerd-b10c13eeeea82c495c9e2fbb07ab448024715fdd55218e22cce6cd815c84bd58.scope
5:pids:/kubepods.slice/kubepods-burstable.slice/kubepods-burstable-pod04f9c4b4_5e71_4a0a_aa3a_f62f089e3f73.slice/cri-containerd-b10c13eeeea82c495c9e2fbb07ab448024715fdd55218e22cce6cd815c84bd58.scope
4:net_cls,net_prio:/kubepods.slice/kubepods-burstable.slice/kubepods-burstable-pod04f9c4b4_5e71_4a0a_aa3a_f62f089e3f73.slice/cri-containerd-b10c13eeeea82c495c9e2fbb07ab448024715fdd55218e22cce6cd815c84bd58.scope
3:devices:/kubepods.slice/kubepods-burstable.slice/kubepods-burstable-pod04f9c4b4_5e71_4a0a_aa3a_f62f089e3f73.slice/cri-containerd-b10c13eeeea82c495c9e2fbb07ab448024715fdd55218e22cce6cd815c84bd58.scope
2:memory:/kubepods.slice/kubepods-burstable.slice/kubepods-burstable-pod04f9c4b4_5e71_4a0a_aa3a_f62f089e3f73.slice/cri-containerd-b10c13eeeea82c495c9e2fbb07ab448024715fdd55218e22cce6cd815c84bd58.scope
1:name=systemd:/kubepods.slice/kubepods-burstable.slice/kubepods-burstable-pod04f9c4b4_5e71_4a0a_aa3a_f62f089e3f73.slice/cri-containerd-b10c13eeeea82c495c9e2fbb07ab448024715fdd55218e22cce6cd815c84bd58.scope
");

            var model = (DockerVendorModel)vendorInfo.GetDockerVendorInfo(mockFileReaderWrapper, true);
            Assert.That(model, Is.Not.Null);
            Assert.That(model.Id, Is.EqualTo("b10c13eeeea82c495c9e2fbb07ab448024715fdd55218e22cce6cd815c84bd58"));
        }


        [Test]
        public void GetVendors_GetDockerVendorInfo_ParsesV1_IfMountinfoDoesNotExist()
        {
            var vendorInfo = new VendorInfo(_configuration, _agentHealthReporter, _environment, _vendorHttpApiRequestor);
            var mockFileReaderWrapper = Mock.Create<IFileReaderWrapper>();
            Mock.Arrange(() => mockFileReaderWrapper.ReadAllText("/proc/self/mountinfo")).Throws<FileNotFoundException>();
            Mock.Arrange(() => mockFileReaderWrapper.ReadAllText("/proc/self/cgroup")).Returns(@"
15:name=systemd:/docker/b9d734e13dc5f508571d975edade94a05dfc637e73a83e11077a39bc11681043
14:misc:/docker/b9d734e13dc5f508571d975edade94a05dfc637e73a83e11077a39bc11681043
13:rdma:/docker/b9d734e13dc5f508571d975edade94a05dfc637e73a83e11077a39bc11681043
12:pids:/docker/b9d734e13dc5f508571d975edade94a05dfc637e73a83e11077a39bc11681043
11:hugetlb:/docker/b9d734e13dc5f508571d975edade94a05dfc637e73a83e11077a39bc11681043
10:net_prio:/docker/b9d734e13dc5f508571d975edade94a05dfc637e73a83e11077a39bc11681043
9:perf_event:/docker/b9d734e13dc5f508571d975edade94a05dfc637e73a83e11077a39bc11681043
8:net_cls:/docker/b9d734e13dc5f508571d975edade94a05dfc637e73a83e11077a39bc11681043
7:freezer:/docker/b9d734e13dc5f508571d975edade94a05dfc637e73a83e11077a39bc11681043
6:devices:/docker/b9d734e13dc5f508571d975edade94a05dfc637e73a83e11077a39bc11681043
5:memory:/docker/b9d734e13dc5f508571d975edade94a05dfc637e73a83e11077a39bc11681043
4:blkio:/docker/b9d734e13dc5f508571d975edade94a05dfc637e73a83e11077a39bc11681043
3:cpuacct:/docker/b9d734e13dc5f508571d975edade94a05dfc637e73a83e11077a39bc11681043
2:cpu:/docker/b9d734e13dc5f508571d975edade94a05dfc637e73a83e11077a39bc11681043
1:cpuset:/docker/b9d734e13dc5f508571d975edade94a05dfc637e73a83e11077a39bc11681043
0::/docker/b9d734e13dc5f508571d975edade94a05dfc637e73a83e11077a39bc11681043");

            var model = (DockerVendorModel)vendorInfo.GetDockerVendorInfo(mockFileReaderWrapper, true);
            Assert.That(model, Is.Not.Null);
            Assert.That(model.Id, Is.EqualTo("b9d734e13dc5f508571d975edade94a05dfc637e73a83e11077a39bc11681043"));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void GetVendors_GetDockerVendorInfo_ParsesEcs_VarV4_IfUnableToParseV1OrV2(bool isLinux)
        {
            // This docker ID is in the Fargate format, but the test is still valid for non-Fargate ECS hosts.
            var dockerId = "1e1698469422439ea356071e581e8545-2769485393";
            SetEnvironmentVariable(AwsEcsMetadataV4EnvVar, $"http://169.254.170.2/v4/{dockerId}", EnvironmentVariableTarget.Process);
            Mock.Arrange(() => _vendorHttpApiRequestor.CallVendorApi(Arg.IsAny<Uri>(), Arg.AnyString, Arg.AnyString, Arg.IsNull<IEnumerable<string>>())).Returns("""
{
    "DockerId": "1e1698469422439ea356071e581e8545-2769485393",
    "Name": "fargateapp",
    "DockerName": "fargateapp",
    "Image": "123456789012.dkr.ecr.us-west-2.amazonaws.com/fargatetest:latest",
    "ImageID": "sha256:1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcd",
    "Labels": {
        "com.amazonaws.ecs.cluster": "arn:aws:ecs:us-west-2:123456789012:cluster/testcluster",
        "com.amazonaws.ecs.container-name": "fargateapp",
        "com.amazonaws.ecs.task-arn": "arn:aws:ecs:us-west-2:123456789012:task/testcluster/1e1698469422439ea356071e581e8545",
        "com.amazonaws.ecs.task-definition-family": "fargatetestapp",
        "com.amazonaws.ecs.task-definition-version": "7"
    },
    "DesiredStatus": "RUNNING",
    "KnownStatus": "RUNNING",
    "Limits": {
        "CPU": 2
    },
    "CreatedAt": "2024-04-25T17:38:31.073208914Z",
    "StartedAt": "2024-04-25T17:38:31.073208914Z",
    "Type": "NORMAL",
    "LogDriver": "awslogs",
    "LogOptions": {
        "awslogs-create-group": "true",
        "awslogs-group": "/ecs/fargatetestapp",
        "awslogs-region": "us-west-2",
        "awslogs-stream": "ecs/fargateapp/1e1698469422439ea356071e581e8545"
    },
    "ContainerARN": "arn:aws:ecs:us-west-2:123456789012:container/testcluster/1e1698469422439ea356071e581e8545/050256a5-a7f3-461c-a16f-aca4eae37b01",
    "Networks": [
        {
            "NetworkMode": "awsvpc",
            "IPv4Addresses": [
                "10.10.10.10"
            ],
            "AttachmentIndex": 0,
            "MACAddress": "06:d7:3f:49:1d:a7",
            "IPv4SubnetCIDRBlock": "10.10.10.0/20",
            "DomainNameServers": [
                "10.10.10.2"
            ],
            "DomainNameSearchList": [
                "us-west-2.compute.internal"
            ],
            "PrivateDNSName": "ip-10-10-10-10.us-west-2.compute.internal",
            "SubnetGatewayIpv4Address": "10.10.10.1/20"
        }
    ],
    "Snapshotter": "overlayfs"
}
""");

            var vendorInfo = new VendorInfo(_configuration, _agentHealthReporter, _environment, _vendorHttpApiRequestor);
            var mockFileReaderWrapper = Mock.Create<IFileReaderWrapper>();
            Mock.Arrange(() => mockFileReaderWrapper.ReadAllText("/proc/self/mountinfo")).Returns("blah blah blah");
            Mock.Arrange(() => mockFileReaderWrapper.ReadAllText("/proc/self/cgroup")).Returns("foo bar baz");

            var model = (DockerVendorModel)vendorInfo.GetDockerVendorInfo(mockFileReaderWrapper, isLinux);
            Assert.That(model, Is.Not.Null);
            Assert.That(model.Id, Is.EqualTo(dockerId));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void GetVendors_GetDockerVendorInfo_ParsesEcs_VarV3_IfUnableToParseV1OrV2(bool isLinux)
        {
            // This docker ID is in the Fargate format, but the test is still valid for non-Fargate ECS hosts.
            var dockerId = "1e1698469422439ea356071e581e8545-2769485393";
            SetEnvironmentVariable(AwsEcsMetadataV3EnvVar, $"http://169.254.170.2/v3/{dockerId}", EnvironmentVariableTarget.Process);
            Mock.Arrange(() => _vendorHttpApiRequestor.CallVendorApi(Arg.IsAny<Uri>(), Arg.AnyString, Arg.AnyString, Arg.IsNull<IEnumerable<string>>())).Returns("""
{
    "DockerId": "1e1698469422439ea356071e581e8545-2769485393",
    "Name": "fargateapp",
    "DockerName": "fargateapp",
    "Image": "123456789012.dkr.ecr.us-west-2.amazonaws.com/fargatetest:latest",
    "ImageID": "sha256:1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcd",
    "Labels": {
        "com.amazonaws.ecs.cluster": "arn:aws:ecs:us-west-2:123456789012:cluster/testcluster",
        "com.amazonaws.ecs.container-name": "fargateapp",
        "com.amazonaws.ecs.task-arn": "arn:aws:ecs:us-west-2:123456789012:task/testcluster/1e1698469422439ea356071e581e8545",
        "com.amazonaws.ecs.task-definition-family": "fargatetestapp",
        "com.amazonaws.ecs.task-definition-version": "7"
    },
    "DesiredStatus": "RUNNING",
    "KnownStatus": "RUNNING",
    "Limits": {
        "CPU": 2
    },
    "CreatedAt": "2024-04-25T17:38:31.073208914Z",
    "StartedAt": "2024-04-25T17:38:31.073208914Z",
    "Type": "NORMAL",
    "Networks": [
        {
            "NetworkMode": "awsvpc",
            "IPv4Addresses": [
                "10.10.10.10"
            ]
        }
    ]
}
""");

            var vendorInfo = new VendorInfo(_configuration, _agentHealthReporter, _environment, _vendorHttpApiRequestor);
            var mockFileReaderWrapper = Mock.Create<IFileReaderWrapper>();
            Mock.Arrange(() => mockFileReaderWrapper.ReadAllText("/proc/self/mountinfo")).Returns("blah blah blah");
            Mock.Arrange(() => mockFileReaderWrapper.ReadAllText("/proc/self/cgroup")).Returns("foo bar baz");

            var model = (DockerVendorModel)vendorInfo.GetDockerVendorInfo(mockFileReaderWrapper, isLinux);
            Assert.That(model, Is.Not.Null);
            Assert.That(model.Id, Is.EqualTo(dockerId));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void GetVendors_GetDockerVendorInfo_ReturnsNull_IfUnableToParseV1OrV2OrEcs(bool isLinux)
        {
            // Not setting the ECS_CONTAINER_METADATA_URI_V4 env var will cause the fargate check to be skipped.

            var vendorInfo = new VendorInfo(_configuration, _agentHealthReporter, _environment, _vendorHttpApiRequestor);
            var mockFileReaderWrapper = Mock.Create<IFileReaderWrapper>();
            Mock.Arrange(() => mockFileReaderWrapper.ReadAllText("/proc/self/mountinfo")).Returns("blah blah blah");
            Mock.Arrange(() => mockFileReaderWrapper.ReadAllText("/proc/self/cgroup")).Returns("foo bar baz");

            var model = (DockerVendorModel)vendorInfo.GetDockerVendorInfo(mockFileReaderWrapper, isLinux);
            Assert.That(model, Is.Null);
        }
#endif

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void GetVendors_DoesNotIncludeAzureFunction_IfUtilizationDetectAzureFunction_IsDisabled(bool enableAzureFunctionUtilization)
        {
            Mock.Arrange(() => _configuration.AzureFunctionModeDetected).Returns(true);
            Mock.Arrange(() => _configuration.AzureFunctionModeEnabled).Returns(true);

            Mock.Arrange(() => _configuration.UtilizationDetectAzureFunction).Returns(enableAzureFunctionUtilization);

            var vendorInfo = new VendorInfo(_configuration, _agentHealthReporter, _environment, _vendorHttpApiRequestor);
            var vendors = vendorInfo.GetVendors();

            if (enableAzureFunctionUtilization)
            {
                Assert.That(vendors, Contains.Key("azurefunction"));
                Assert.That(vendors["azurefunction"], Is.Not.Null);
            }
            else
            {
                Assert.That(vendors, Does.Not.ContainKey("azurefunction"));
            }
        }

        [Test]
        [TestCase(true, false)]
        [TestCase(false, true)]
        public void GetAzureFunctionVendorInfo_ReturnsAzureFunctionModel_WhenAzureMode_IsEnabled(bool azureFunctionModeEnabled, bool expectNull)
        {
            Mock.Arrange(() => _configuration.AzureFunctionModeDetected).Returns(azureFunctionModeEnabled);
            Mock.Arrange(() => _configuration.AzureFunctionModeEnabled).Returns(azureFunctionModeEnabled);

            Mock.Arrange(() => _configuration.AzureFunctionRegion).Returns("North Central US");
            Mock.Arrange(() => _configuration.AzureFunctionResourceId).Returns("AzureResourceId");

            var vendorInfo = new VendorInfo(_configuration, _agentHealthReporter, _environment, _vendorHttpApiRequestor);
            var model = (AzureFunctionVendorModel)vendorInfo.GetAzureFunctionVendorInfo();

            if (expectNull)
            {
                Assert.That(model, Is.Null);
            }
            else
            {
                Assert.That(model, Is.Not.Null);
                Assert.That(model.AppName, Is.EqualTo("AzureResourceId"));
                Assert.That(model.CloudRegion, Is.EqualTo("North Central US"));
            }
        }

        [Test]
        public void GetAzureFunctionVendorInfo_ReturnsNull_WhenRegionIsNotAvailable()
        {
            Mock.Arrange(() => _configuration.AzureFunctionModeDetected).Returns(true);
            Mock.Arrange(() => _configuration.AzureFunctionModeEnabled).Returns(true);

            Mock.Arrange(() => _configuration.AzureFunctionRegion).Returns((string)null);
            Mock.Arrange(() => _configuration.AzureFunctionResourceId).Returns("AzureResourceId");

            var vendorInfo = new VendorInfo(_configuration, _agentHealthReporter, _environment, _vendorHttpApiRequestor);
            var model = (AzureFunctionVendorModel)vendorInfo.GetAzureFunctionVendorInfo();

            Assert.That(model, Is.Null);
        }
        [Test]
        public void GetAzureFunctionVendorInfo_ReturnsNull_WhenResourceIdIsNotAvailable()
        {
            Mock.Arrange(() => _configuration.AzureFunctionModeDetected).Returns(true);
            Mock.Arrange(() => _configuration.AzureFunctionModeEnabled).Returns(true);

            Mock.Arrange(() => _configuration.AzureFunctionRegion).Returns("North Central US");
            Mock.Arrange(() => _configuration.AzureFunctionResourceId).Returns((string)null);

            var vendorInfo = new VendorInfo(_configuration, _agentHealthReporter, _environment, _vendorHttpApiRequestor);
            var model = (AzureFunctionVendorModel)vendorInfo.GetAzureFunctionVendorInfo();

            Assert.That(model, Is.Null);
        }



        private void SetEnvironmentVariable(string variableName, string value, EnvironmentVariableTarget environmentVariableTarget)
        {
            Mock.Arrange(() => _environment.GetEnvironmentVariable(variableName, environmentVariableTarget)).Returns(value);
            Mock.Arrange(() => _environment.GetEnvironmentVariable(variableName)).Returns(value);
        }
    }
}
