// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.IO;
using System.Linq;
using NewRelic.Agent.Core.AgentHealth;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.TestUtilities;
using NewRelic.SystemInterfaces;
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

            var vendorInfo = new VendorInfo(_configuration, _agentHealthReporter, _environment, _vendorHttpApiRequestor);
            var vendors = vendorInfo.GetVendors();
            Assert.IsFalse(vendors.Any());
        }

        [Test]
        public void GetVendors_Returns_Empty_Dictionary_When_Detect_False()
        {
            var vendorInfo = new VendorInfo(_configuration, _agentHealthReporter, _environment, _vendorHttpApiRequestor);
            var vendors = vendorInfo.GetVendors();
            Assert.IsFalse(vendors.Any());
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
                Assert.Null(result);
            }
            else
            {
                Assert.True(result.Equals(expectedResponse));
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

            Assert.NotNull(model);
            Assert.True(model.InstanceId == "i-1234567890abcdef0");
            Assert.True(model.InstanceType == "t1.micro");
            Assert.True(model.AvailabilityZone == "us - east - 1d");
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

            Assert.IsNull(model);
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

            Assert.Null(model);
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

            Assert.NotNull(model);
            Assert.True(model.Location == "CentralUS");
            Assert.True(model.Name == "IMDSCanary");
            Assert.True(model.VmId == "5c08b38e-4d57-4c23-ac45-aca61037f084");
            Assert.True(model.VmSize == "Standard_DS2");
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

            Assert.Null(model);
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

            Assert.Null(model);
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

            Assert.NotNull(model);
            Assert.True(model.Id == "3161347020215157000");
            Assert.True(model.MachineType == "custom - 1 - 1024");
            Assert.True(model.Name == "aef-default-20170501t160547-7gh8");
            Assert.True(model.Zone == "us-central1-c");
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

            Assert.Null(model);
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

            Assert.Null(model);
        }

        [Test]
        public void GetVendors_GetPcfVendorInfo_Complete()
        {
            SetEnvironmentVariable(PcfInstanceGuid, "b977d090-83db-4bdb-793a-bb77", EnvironmentVariableTarget.Process);
            SetEnvironmentVariable(PcfInstanceIp, "10.10.147.130", EnvironmentVariableTarget.Process);
            SetEnvironmentVariable(PcfMemoryLimit, "1024m", EnvironmentVariableTarget.Process);

            var vendorInfo = new VendorInfo(_configuration, _agentHealthReporter, _environment, _vendorHttpApiRequestor);
            var model = (PcfVendorModel)vendorInfo.GetPcfVendorInfo();

            Assert.NotNull(model);
            Assert.True(model.CfInstanceGuid == "b977d090-83db-4bdb-793a-bb77");
            Assert.True(model.CfInstanceIp == "10.10.147.130");
            Assert.True(model.MemoryLimit == "1024m");
        }

        [Test]
        public void GetVendors_GetPcfVendorInfo_None()
        {
            SetEnvironmentVariable(PcfInstanceGuid, null, EnvironmentVariableTarget.Process);
            SetEnvironmentVariable(PcfInstanceIp, null, EnvironmentVariableTarget.Process);
            SetEnvironmentVariable(PcfMemoryLimit, null, EnvironmentVariableTarget.Process);

            var vendorInfo = new VendorInfo(_configuration, _agentHealthReporter, _environment, _vendorHttpApiRequestor);
            var model = (PcfVendorModel)vendorInfo.GetPcfVendorInfo();

            Assert.Null(model);
        }

        [Test]
        public void GetVendors_GetKubernetesVendorInfo_Complete()
        {
            var serviceHost = "10.96.0.1";
            SetEnvironmentVariable(KubernetesServiceHost, serviceHost, EnvironmentVariableTarget.Process);

            var vendorInfo = new VendorInfo(_configuration, _agentHealthReporter, _environment, _vendorHttpApiRequestor);
            var model = (KubernetesVendorModel)vendorInfo.GetKubernetesInfo();

            Assert.NotNull(model);
            Assert.True(model.KubernetesServiceHost == serviceHost);
        }

        [Test]
        public void GetVendors_GetKubernetesVendorInfo_None()
        {
            SetEnvironmentVariable(KubernetesServiceHost, null, EnvironmentVariableTarget.Process);

            var vendorInfo = new VendorInfo(_configuration, _agentHealthReporter, _environment, _vendorHttpApiRequestor);
            var model = (KubernetesVendorModel)vendorInfo.GetKubernetesInfo();

            Assert.Null(model);
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

            var model = (DockerVendorModel)vendorInfo.GetDockerVendorInfo(mockFileReaderWrapper);
            Assert.NotNull(model);
            Assert.AreEqual("adf04870aa0a9f01fb712e283765ee5d7c7b1c1c0ad8ebfdea20a8bb3ae382fb", model.Id);
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

            var model = (DockerVendorModel)vendorInfo.GetDockerVendorInfo(mockFileReaderWrapper);
            Assert.NotNull(model);
            Assert.AreEqual("b9d734e13dc5f508571d975edade94a05dfc637e73a83e11077a39bc11681043", model.Id);
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

            var model = (DockerVendorModel)vendorInfo.GetDockerVendorInfo(mockFileReaderWrapper);
            Assert.NotNull(model);
            Assert.AreEqual("b9d734e13dc5f508571d975edade94a05dfc637e73a83e11077a39bc11681043", model.Id);
        }

        [Test]
        public void GetVendors_GetDockerVendorInfo_ReturnsNull_IfUnableToParseV1OrV2()
        {
            var vendorInfo = new VendorInfo(_configuration, _agentHealthReporter, _environment, _vendorHttpApiRequestor);
            var mockFileReaderWrapper = Mock.Create<IFileReaderWrapper>();
            Mock.Arrange(() => mockFileReaderWrapper.ReadAllText("/proc/self/mountinfo")).Returns("blah blah blah");
            Mock.Arrange(() => mockFileReaderWrapper.ReadAllText("/proc/self/cgroup")).Returns("foo bar baz");

            var model = (DockerVendorModel)vendorInfo.GetDockerVendorInfo(mockFileReaderWrapper);
            Assert.Null(model);
        }
#endif
        private void SetEnvironmentVariable(string variableName, string value, EnvironmentVariableTarget environmentVariableTarget)
        {
            Mock.Arrange(() => _environment.GetEnvironmentVariable(variableName, environmentVariableTarget)).Returns(value);
            Mock.Arrange(() => _environment.GetEnvironmentVariable(variableName)).Returns(value);
        }
    }
}
