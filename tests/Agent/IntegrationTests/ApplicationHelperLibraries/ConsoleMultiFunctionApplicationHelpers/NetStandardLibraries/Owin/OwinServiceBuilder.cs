// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;

namespace MultiFunctionApplicationHelpers.NetStandardLibraries.Owin
{
    public class OwinServiceBuilder
    {
        private IStartup _startup;
        private List<Type> _controllers;

        public OwinServiceBuilder()
        {
            _controllers = new List<Type>();
        }

        public OwinServiceBuilder AddStartup(IStartup startup)
        {
            _startup = startup;
            return this;
        }

        public OwinServiceBuilder RegisterController(Type controller)
        {
            _controllers.Add(controller);
            return this;
        }

        public OwinService Build()
        {
            var service = new OwinService();
            if (_startup == null)
            {
                service.AddStartup(new DefaultStartup());
            }
            else
            {
                service.AddStartup(_startup);
            }

            foreach (var controller in _controllers)
            {
                service.RegisterController(controller);
            }

            return service;
        }
    }
}
