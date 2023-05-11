// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.Collections.Generic;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;

namespace NewRelic.Agent.IntegrationTests.RemoteServiceFixtures
{
    public class ServiceStackApplicationFixture : RemoteApplicationFixture
    {

        public ServiceStackApplicationFixture() : base(new RemoteWebApplication("ServiceStackApplication", ApplicationType.Bounded))
        {
        }

        public void GetHello()
        {
            var address = $"http://{DestinationServerName}:{Port}/hello/Worked";//"?format=json";
            GetJsonAndAssertEqual(address, new HelloResponse("Worked"), new List<KeyValuePair<string, string>>() { new KeyValuePair<string, string>("Accept", "application/json")});
        }

        public class HelloResponse
        {
            public string Result { get; }

            public HelloResponse(string result)
            {
                Result = result;
            }

            public override bool Equals(object obj)
            {
                return Equals(obj as HelloResponse);
            }

            protected bool Equals(HelloResponse other)
            {
                return string.Equals(Result, other.Result);
            }

            public override int GetHashCode()
            {
                return (Result != null ? Result.GetHashCode() : 0);
            }
        }
    }
}
