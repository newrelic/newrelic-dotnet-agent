// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


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

            GetStringAndAssertIsNotNull(address);
        }

        public void Find()
        {
            var address = $"http://{DestinationServerName}:{Port}/api/MongoDB/find";

            GetStringAndAssertIsNotNull(address);

        }

        public void FindOne()
        {
            var address = $"http://{DestinationServerName}:{Port}/api/MongoDB/findone";

            GetStringAndAssertIsNotNull(address);
        }

        public void FindAll()
        {
            var address = $"http://{DestinationServerName}:{Port}/api/MongoDB/findall";

            GetStringAndAssertIsNotNull(address);
        }

        public void OrderedBulkInsert()
        {
            var address = $"http://{DestinationServerName}:{Port}/api/MongoDB/orderedbulkinsert";

            GetStringAndAssertIsNotNull(address);
        }

        public void Update()
        {
            var address = $"http://{DestinationServerName}:{Port}/api/MongoDB/update";

            GetStringAndAssertIsNotNull(address);
        }

        public void Remove()
        {
            var address = $"http://{DestinationServerName}:{Port}/api/MongoDB/remove";

            GetStringAndAssertIsNotNull(address);
        }

        public void RemoveAll()
        {
            var address = $"http://{DestinationServerName}:{Port}/api/MongoDB/removeAll";

            GetStringAndAssertIsNotNull(address);
        }

        public void Drop()
        {
            var address = $"http://{DestinationServerName}:{Port}/api/MongoDB/drop";

            GetStringAndAssertIsNotNull(address);
        }

        public void FindAndModify()
        {
            var address = $"http://{DestinationServerName}:{Port}/api/MongoDB/findandmodify";

            GetStringAndAssertIsNotNull(address);
        }


        public void FindAndRemove()
        {
            var address = $"http://{DestinationServerName}:{Port}/api/MongoDB/findandremove";

            GetStringAndAssertIsNotNull(address);
        }

        public void CreateIndex()
        {
            var address = $"http://{DestinationServerName}:{Port}/api/MongoDB/createindex";

            GetStringAndAssertIsNotNull(address);
        }

        public void GetIndexes()
        {
            var address = $"http://{DestinationServerName}:{Port}/api/MongoDB/getindexes";

            GetStringAndAssertIsNotNull(address);
        }

        public void IndexExistsByName()
        {
            var address = $"http://{DestinationServerName}:{Port}/api/MongoDB/indexexistsbyname";

            GetStringAndAssertIsNotNull(address);
        }

        public void Aggregate()
        {
            var address = $"http://{DestinationServerName}:{Port}/api/MongoDB/aggregate";

            GetStringAndAssertIsNotNull(address);
        }

        public void Validate()
        {
            var address = $"http://{DestinationServerName}:{Port}/api/MongoDB/validate";

            GetStringAndAssertIsNotNull(address);
        }

        public void ParallelScanAs()
        {
            var address = $"http://{DestinationServerName}:{Port}/api/MongoDB/parallelscanas";

            GetStringAndAssertIsNotNull(address);
        }

        public void CreateCollection()
        {
            var address = $"http://{DestinationServerName}:{Port}/api/MongoDB/createcollection";

            GetStringAndAssertIsNotNull(address);

            //Wait for harvest?
        }

    }
}
