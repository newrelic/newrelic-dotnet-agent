using System;
using System.Linq;
using System.Reflection;
using JetBrains.Annotations;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Config;
using NewRelic.Agent.Core.Events;
using NewRelic.Agent.Core.Logging;
using NewRelic.Agent.Core.Requests;
using NewRelic.Agent.Core.Utilities;
using NewRelic.SystemInterfaces;
using NewRelic.SystemInterfaces.Web;

namespace NewRelic.Agent.Core.Configuration
{
    public class ConfigurationService : IConfigurationService, IDisposable
    {
        [NotNull]
        private readonly IEnvironment _environment;

        [NotNull]
        private configuration _localConfiguration = new configuration();

        [NotNull]
        private ServerConfiguration _serverConfiguration = ServerConfiguration.GetDefault();

        [NotNull]
        private RunTimeConfiguration _runTimeConfiguration = new RunTimeConfiguration();

        [NotNull]
        private readonly Subscriptions _subscriptions = new Subscriptions();

        [NotNull]
        private readonly IProcessStatic _processStatic;
        [NotNull]
        private readonly IHttpRuntimeStatic _httpRuntimeStatic;
        [NotNull]
        private readonly IConfigurationManagerStatic _configurationManagerStatic;

        public IConfiguration Configuration { get; private set; }

        public ConfigurationService([NotNull] IEnvironment environment, [NotNull] IProcessStatic processStatic, [NotNull] IHttpRuntimeStatic httpRuntimeStatic, [NotNull] IConfigurationManagerStatic configurationManagerStatic)
        {
            _environment = environment;
            _processStatic = processStatic;
            _httpRuntimeStatic = httpRuntimeStatic;
            _configurationManagerStatic = configurationManagerStatic;

            Configuration = new InternalConfiguration(_environment, _localConfiguration, _serverConfiguration, _runTimeConfiguration, _processStatic, _httpRuntimeStatic, _configurationManagerStatic);

            _subscriptions.Add<ConfigurationDeserializedEvent>(OnConfigurationDeserialized);
            _subscriptions.Add<ServerConfigurationUpdatedEvent>(OnServerConfigurationUpdated);
            _subscriptions.Add<AppNameUpdateEvent>(OnAppNameUpdate);
            _subscriptions.Add<GetCurrentConfigurationRequest, IConfiguration>(OnGetCurrentConfiguration);
        }

        private void OnConfigurationDeserialized([NotNull] ConfigurationDeserializedEvent configurationDeserializedEvent)
        {
            _localConfiguration = configurationDeserializedEvent.Configuration;
            UpdateLogLevel(_localConfiguration);
            UpdateAndPublishConfiguration(ConfigurationUpdateSource.Local);
        }

        private static void UpdateLogLevel([NotNull] configuration localConfiguration)
        {
            var hierarchy = log4net.LogManager.GetRepository(Assembly.GetCallingAssembly()) as log4net.Repository.Hierarchy.Hierarchy;
            var logger = hierarchy.Root;

            var logLevel = logger.Hierarchy.LevelMap[localConfiguration.LogConfig.LogLevel];
            if (logLevel != null && logLevel != logger.Level)
            {
                Log.InfoFormat("The log level was updated to {0}", logLevel);
                logger.Level = logLevel;
            }
        }

        private void OnServerConfigurationUpdated([NotNull] ServerConfigurationUpdatedEvent serverConfigurationUpdatedEvent)
        {
            try
            {
                _serverConfiguration = serverConfigurationUpdatedEvent.Configuration;
                UpdateAndPublishConfiguration(ConfigurationUpdateSource.Server);
            }
            catch (Exception exception)
            {
                Log.Error($"Unable to parse the Configuration data from the server so no server side configuration was applied: {exception}");
            }
        }

        private void OnAppNameUpdate([NotNull] AppNameUpdateEvent appNameUpdateEvent)
        {
            if (_runTimeConfiguration.ApplicationNames.SequenceEqual(appNameUpdateEvent.AppNames))
                return;

            _runTimeConfiguration = new RunTimeConfiguration(appNameUpdateEvent.AppNames);
            UpdateAndPublishConfiguration(ConfigurationUpdateSource.RunTime);
        }

        private void UpdateAndPublishConfiguration(ConfigurationUpdateSource configurationUpdateSource)
        {
            Configuration = new InternalConfiguration(_environment, _localConfiguration, _serverConfiguration, _runTimeConfiguration, _processStatic, _httpRuntimeStatic, _configurationManagerStatic);

            var configurationUpdatedEvent = new ConfigurationUpdatedEvent(Configuration, configurationUpdateSource);
            EventBus<ConfigurationUpdatedEvent>.Publish(configurationUpdatedEvent);
        }

        private void OnGetCurrentConfiguration([NotNull] GetCurrentConfigurationRequest eventData, [NotNull] RequestBus<GetCurrentConfigurationRequest, IConfiguration>.ResponseCallback callback)
        {
            callback(Configuration);
        }

        public void Dispose()
        {
            _subscriptions.Dispose();
        }
    }
}
