// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using NewRelic.Agent.ContainerIntegrationTests.Applications;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using Newtonsoft.Json;

namespace NewRelic.Agent.ContainerIntegrationTests.Fixtures
{
    public abstract class LambdaTestFixtureBase : RemoteApplicationFixture
    {
        protected LambdaTestFixtureBase(
            string distroTag,
            ContainerApplication.Architecture containerArchitecture,
            string dockerfile,
            string dotnetVersion,
            string dockerComposeFile = "docker-compose-lambda.yml") :
            base(new ContainerApplication(distroTag, containerArchitecture, dotnetVersion, dockerfile, dockerComposeFile))
        {
        }
        public virtual void ExerciseApplication()
        {
            var address = $"http://localhost:{Port}//2015-03-31/functions/function/invocations";

            using var client = new HttpClient();

            string foo = "foo";

            string json = JsonConvert.SerializeObject(foo);

            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var resp = client.PostAsync(address, content).GetAwaiter().GetResult();

        }

        public void Delay(int seconds)
        {
            Task.Delay(TimeSpan.FromSeconds(seconds)).GetAwaiter().GetResult();
        }
    }

    public class LambdaDotNet7TestFixture : LambdaTestFixtureBase
    {
        private const string Dockerfile = "LambdaFunctionTestApp/Dockerfile";
        private const ContainerApplication.Architecture Architecture = ContainerApplication.Architecture.X64;
        private const string DistroTag = "bullseye-slim"; // not used
        private const string DotnetVersion = "7.0"; // not used, presently

        public LambdaDotNet7TestFixture() : base(DistroTag, Architecture, Dockerfile, DotnetVersion) { }
    }

}
