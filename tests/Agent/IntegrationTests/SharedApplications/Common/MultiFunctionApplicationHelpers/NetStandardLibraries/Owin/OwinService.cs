// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using Microsoft.Owin.Hosting;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MultiFunctionApplicationHelpers.NetStandardLibraries.Owin
{
    public class OwinService
    {
        private IStartup _startup;
        private IDisposable _service;
        private List<Type> _controllers;

        internal OwinService()
        {
            _controllers = new List<Type>();
        }

        internal OwinService(IStartup startup) : this()
        {
            _startup = startup;
        }

        public void StartService(string server, int port)
        {
            _service = WebApp.Start(url: $"http://{server}:{port}/", _startup.Configuration);
            Task.Delay(7000).Wait();
        }

        public void StopService()
        {
            _service.Dispose();
        }

        internal void AddStartup(IStartup startup)
        {
            _startup = startup;
        }

        internal void RegisterController(Type controller)
        {
            _controllers.Add(controller);
        }
    }
}
