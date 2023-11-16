// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using MultiFunctionApplicationHelpers.NetStandardLibraries.Owin;
using NewRelic.Agent.IntegrationTests.Shared.ReflectionHelpers;
using System;
using System.Net.Http;

namespace MultiFunctionApplicationHelpers.NetStandardLibraries.RestSharp
{
    [Library]
    public class RestSharpService
    {
        private OwinService _owinService;

        /// <summary>
        /// Starts the RestSharp Test Service with a specific port and path
        /// </summary>
        /// <param name="server"></param>
        /// <param name="port"></param>
        /// <param name="relativePath"></param>
        [LibraryMethod]
        public void StartService(string server, int port)
        {
            try
            {
                ConsoleMFLogger.Info("Starting RestSharp Test Service.");

                // build owin service
                _owinService = new OwinServiceBuilder()
                    .RegisterController(typeof(RestAPIController))
                    .AddStartup(new FullRoutesStartup())
                    .Build();

                // Start OWIN host 
                _owinService.StartService(server, port);
            }
            catch (Exception ex)
            {
                ConsoleMFLogger.Error(ex);
            }
        }

        /// <summary>
        /// Stops the RestSharp Test Service
        /// </summary>
        [LibraryMethod]
        public void StopService()
        {
            ConsoleMFLogger.Info("Stopping RestSharp Test Service");
            _owinService.StopService();
        }
    }
}
