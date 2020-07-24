using JetBrains.Annotations;
using NewRelic.SystemInterfaces;
using NewRelic.SystemInterfaces.Web;

namespace NewRelic.Agent.Core.Configuration
{
    /// <summary>
    /// IConfiguration implementation for use by ConfigurationService only.  Other classes should use DefaultConfiguration and listen to ConfigurationUpdatedEvent.
    /// </summary>
    internal class InternalConfiguration : DefaultConfiguration
    {
        public InternalConfiguration([NotNull] IEnvironment environment, Config.configuration localConfiguration, ServerConfiguration serverConfiguration, RunTimeConfiguration runTimeConfiguration, [NotNull] IProcessStatic processStatic, [NotNull] IHttpRuntimeStatic httpRuntimeStatic, [NotNull] IConfigurationManagerStatic configurationManagerStatic) :
            base(environment, localConfiguration, serverConfiguration, runTimeConfiguration, processStatic, httpRuntimeStatic, configurationManagerStatic)
        { }
    }
}
