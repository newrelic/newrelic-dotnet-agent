// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.IO;
using System.Security.AccessControl;
using System.Threading.Tasks;
using Amazon;
using Amazon.Lambda.Model;
using NewRelic.Agent.IntegrationTests.Shared.ReflectionHelpers;
using NewRelic.Api.Agent;

namespace MultiFunctionApplicationHelpers.NetStandardLibraries.AwsSdk
{
    [Library]
    public class InvokeLambdaExerciser
    {
        [LibraryMethod]
        [Transaction]
        public void InvokeLambdaSync(string function, string payload)
        {
#if NETFRAMEWORK
            var client = new Amazon.Lambda.AmazonLambdaClient(RegionEndpoint.USWest2);
            var request = new Amazon.Lambda.Model.InvokeRequest
            {
                FunctionName = function,
                Payload = payload
            };

            // Note that we aren't invoking a lambda that exists! This is guaranteed to fail, but all we care
            // about is that the agent is able to instrument the call.
            try
            {
                var response = client.Invoke(request);
            }
            catch
            {
            }
#else
            throw new Exception($"Synchronous calls are only supported on .NET Framework!");
#endif
        }

        [LibraryMethod]
        [Transaction]
        public async Task<string> InvokeLambdaAsync(string function, string payload)
        {
            var client = new Amazon.Lambda.AmazonLambdaClient(RegionEndpoint.USWest2);
            var request = new Amazon.Lambda.Model.InvokeRequest
            {
                FunctionName = function,
                Payload = payload
            };

            // Note that we aren't invoking a lambda that exists! This is guaranteed to fail, but all we care
            // about is that the agent is able to instrument the call.
            try
            {
                var response = await client.InvokeAsync(request);
                MemoryStream stream = response.Payload;
                string returnValue = System.Text.Encoding.UTF8.GetString(stream.ToArray());
                return returnValue;
            }
            catch
            {
            }
            return null;
        }

        [LibraryMethod]
        [Transaction]
        public async Task<string> InvokeLambdaAsyncWithQualifier(string function, string qualifier, string payload)
        {
            var client = new Amazon.Lambda.AmazonLambdaClient(RegionEndpoint.USWest2);
            var request = new Amazon.Lambda.Model.InvokeRequest
            {
                FunctionName = function,
                Qualifier = qualifier,
                Payload = payload
            };

            // Note that we aren't invoking a lambda that exists! This is guaranteed to fail, but all we care
            // about is that the agent is able to instrument the call.
            try
            {
                var response = await client.InvokeAsync(request);
                MemoryStream stream = response.Payload;
                string returnValue = System.Text.Encoding.UTF8.GetString(stream.ToArray());
                return returnValue;
            }
            catch
            {
            }
            return null;
        }
    }
}
