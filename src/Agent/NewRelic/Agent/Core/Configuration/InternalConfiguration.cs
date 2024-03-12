// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Core.Config;
using NewRelic.SystemInterfaces;
using NewRelic.SystemInterfaces.Web;

namespace NewRelic.Agent.Core.Configuration
{
    /// <summary>
    /// IConfiguration implementation for use by ConfigurationService only.  Other classes should use DefaultConfiguration and listen to ConfigurationUpdatedEvent.
    /// </summary>
    internal class InternalConfiguration : DefaultConfiguration
    {
        public InternalConfiguration(IEnvironment environment, configuration localConfiguration, ServerConfiguration serverConfiguration, RunTimeConfiguration runTimeConfiguration, SecurityPoliciesConfiguration securityPoliciesConfiguration, IBootstrapConfiguration bootstrapConfiguration, IProcessStatic processStatic, IHttpRuntimeStatic httpRuntimeStatic, IConfigurationManagerStatic configurationManagerStatic, IDnsStatic dnsStatic) :
            base(environment, localConfiguration, serverConfiguration, runTimeConfiguration, securityPoliciesConfiguration, bootstrapConfiguration, processStatic, httpRuntimeStatic, configurationManagerStatic, dnsStatic)
        { }
    }
}
