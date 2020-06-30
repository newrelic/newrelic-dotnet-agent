/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/

using System.Net;
using Xunit;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using NewRelic.Agent.IntegrationTests.Shared;

namespace NewRelic.Agent.UnboundedIntegrationTests.RemoteServiceFixtures
{
    public class MongoDbApplicationFixture : RemoteApplicationFixture
    {
        public MongoDbApplicationFixture() : base(new RemoteWebApplication("MongoDBApplication", ApplicationType.Unbounded))
        {
        }

        public void Insert()
        {
            var address = $"http://{DestinationServerName}:{Port}/api/MongoDB/insert";

            using (var webClient = new WebClient())
            {
                var responseBody = webClient.DownloadString(address);
                Assert.NotNull(responseBody);
            }
        }

        public void Find()
        {
            var address = $"http://{DestinationServerName}:{Port}/api/MongoDB/find";

            using (var webClient = new WebClient())
            {
                var responseBody = webClient.DownloadString(address);
                Assert.NotNull(responseBody);
            }

        }

        public void FindOne()
        {
            var address = $"http://{DestinationServerName}:{Port}/api/MongoDB/findone";

            using (var webClient = new WebClient())
            {
                var responseBody = webClient.DownloadString(address);
                Assert.NotNull(responseBody);
            }
        }

        public void FindAll()
        {
            var address = $"http://{DestinationServerName}:{Port}/api/MongoDB/findall";

            using (var webClient = new WebClient())
            {
                var responseBody = webClient.DownloadString(address);
                Assert.NotNull(responseBody);
            }
        }

        public void OrderedBulkInsert()
        {
            var address = $"http://{DestinationServerName}:{Port}/api/MongoDB/orderedbulkinsert";

            using (var webClient = new WebClient())
            {
                var responseBody = webClient.DownloadString(address);
                Assert.NotNull(responseBody);
            }
        }

        public void Update()
        {
            var address = $"http://{DestinationServerName}:{Port}/api/MongoDB/update";

            using (var webClient = new WebClient())
            {
                var responseBody = webClient.DownloadString(address);
                Assert.NotNull(responseBody);
            }
        }

        public void Remove()
        {
            var address = $"http://{DestinationServerName}:{Port}/api/MongoDB/remove";

            using (var webClient = new WebClient())
            {
                var responseBody = webClient.DownloadString(address);
                Assert.NotNull(responseBody);
            }
        }

        public void RemoveAll()
        {
            var address = $"http://{DestinationServerName}:{Port}/api/MongoDB/removeAll";

            using (var webClient = new WebClient())
            {
                var responseBody = webClient.DownloadString(address);
                Assert.NotNull(responseBody);
            }
        }

        public void Drop()
        {
            var address = $"http://{DestinationServerName}:{Port}/api/MongoDB/drop";

            using (var webClient = new WebClient())
            {
                var responseBody = webClient.DownloadString(address);
                Assert.NotNull(responseBody);
            }
        }

        public void FindAndModify()
        {
            var address = $"http://{DestinationServerName}:{Port}/api/MongoDB/findandmodify";

            using (var webClient = new WebClient())
            {
                var responseBody = webClient.DownloadString(address);
                Assert.NotNull(responseBody);
            }
        }


        public void FindAndRemove()
        {
            var address = $"http://{DestinationServerName}:{Port}/api/MongoDB/findandremove";

            using (var webClient = new WebClient())
            {
                var responseBody = webClient.DownloadString(address);
                Assert.NotNull(responseBody);
            }
        }

        public void CreateIndex()
        {
            var address = $"http://{DestinationServerName}:{Port}/api/MongoDB/createindex";

            using (var webClient = new WebClient())
            {
                var responseBody = webClient.DownloadString(address);
                Assert.NotNull(responseBody);
            }
        }

        public void GetIndexes()
        {
            var address = $"http://{DestinationServerName}:{Port}/api/MongoDB/getindexes";

            using (var webClient = new WebClient())
            {
                var responseBody = webClient.DownloadString(address);
                Assert.NotNull(responseBody);
            }
        }

        public void IndexExistsByName()
        {
            var address = $"http://{DestinationServerName}:{Port}/api/MongoDB/indexexistsbyname";

            using (var webClient = new WebClient())
            {
                var responseBody = webClient.DownloadString(address);
                Assert.NotNull(responseBody);
            }
        }

        public void Aggregate()
        {
            var address = $"http://{DestinationServerName}:{Port}/api/MongoDB/aggregate";

            using (var webClient = new WebClient())
            {
                var responseBody = webClient.DownloadString(address);
                Assert.NotNull(responseBody);
            }
        }

        public void Validate()
        {
            var address = $"http://{DestinationServerName}:{Port}/api/MongoDB/validate";

            using (var webClient = new WebClient())
            {
                var responseBody = webClient.DownloadString(address);
                Assert.NotNull(responseBody);
            }
        }

        public void ParallelScanAs()
        {
            var address = $"http://{DestinationServerName}:{Port}/api/MongoDB/parallelscanas";

            using (var webClient = new WebClient())
            {
                var responseBody = webClient.DownloadString(address);
                Assert.NotNull(responseBody);
            }
        }

        public void CreateCollection()
        {
            var address = $"http://{DestinationServerName}:{Port}/api/MongoDB/createcollection";

            using (var webClient = new WebClient())
            {
                var responseBody = webClient.DownloadString(address);
                Assert.NotNull(responseBody);
            }

            //Wait for harvest?
        }

    }
}
