/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
using NewRelic.SystemInterfaces;
using NewRelic.SystemInterfaces.Web;

namespace NewRelic.Agent.Core.Configuration
{
    /// <summary>
    /// IConfiguration implementation for use by ConfigurationService only.  Other classes should use DefaultConfiguration and listen to ConfigurationUpdatedEvent.
    /// </summary>
    internal class InternalConfiguration : DefaultConfiguration
    {
        public InternalConfiguration(IEnvironment environment, Config.configuration localConfiguration, ServerConfiguration serverConfiguration, RunTimeConfiguration runTimeConfiguration, IProcessStatic processStatic, IHttpRuntimeStatic httpRuntimeStatic, IConfigurationManagerStatic configurationManagerStatic) :
            base(environment, localConfiguration, serverConfiguration, runTimeConfiguration, processStatic, httpRuntimeStatic, configurationManagerStatic)
        { }
    }
}
