// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using MultiFunctionApplicationHelpers.NetStandardLibraries.Owin;
using NewRelic.Agent.IntegrationTests.Shared.ReflectionHelpers;
using System;

namespace MultiFunctionApplicationHelpers.NetStandardLibraries.W3C
{
    [Library]
    public class W3CTestService
    {
        private OwinService _owinService;

        /// <summary>
        /// Starts the W3C Test Service with a specific port and path
        /// </summary>
        [LibraryMethod]
        public void StartService(string server, int port)
        {
            try
            {
                ConsoleMFLogger.Info("Starting W3C Test Service.");

                // build owin service
                _owinService = new OwinServiceBuilder()
                    .RegisterController(typeof(W3CController))
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
        /// Stops the W3C Test Service
        /// </summary>
        [LibraryMethod]
        public void StopService()
        {
            ConsoleMFLogger.Info("Stopping W3C Test Service");
            _owinService.StopService();
        }
    }
}
