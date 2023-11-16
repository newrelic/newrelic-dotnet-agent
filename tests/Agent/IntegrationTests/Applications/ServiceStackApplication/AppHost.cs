// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using Funq;
using ServiceStack;
using ServiceStack.Seq.RequestLogsFeature;

namespace ServiceStackApplication
{
    //VS.NET Template Info: https://servicestack.net/vs-templates/EmptyAspNet
    public class AppHost : AppHostBase
    {
        /// <summary>
        /// Base constructor requires a Name and Assembly where web service implementation is located
        /// </summary>
        public AppHost()
            : base("ServiceStackApplication", typeof(AppHost).Assembly) { }

        /// <summary>
        /// Application specific configuration
        /// This method should initialize any IoC resources utilized by your web service classes.
        /// </summary>
        public override void Configure(Container container)
        {

            // This plugin attempts to serialize everything in HttpContext using ServiceStack.Text
            // It tests that we do not reintroduce a StackOverflow issue that can crash apps.
            // We don't formally take responsibility over this but we do our best to do no harm.
            Plugins.Add(new SeqRequestLogsFeature { SeqUrl = "http://127.0.0.1:5341" });
        }
    }
}
