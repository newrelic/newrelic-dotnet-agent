// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Core.Events;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Core.Configuration;
using NewRelic.Agent.Extensions.Logging;
using NewRelic.Agent.Core.SharedInterfaces;
using System;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
#if NETSTANDARD2_0
using System.Reflection;
#else
using System.Configuration;
using System.Web;
#endif

namespace NewRelic.Agent.Core.Config
{

    /// <summary>
    /// Reads configuration data
    /// </summary>
    public static class ConfigurationLoader
    {
        private const string NewRelicConfigFileName = "newrelic.config";
        private static readonly ProcessStatic _processStatic = new ProcessStatic();

        #region Unit test helpers

        // These fields and methods exists and is public so that the test projects can replace the functionality
        // without requiring a redesign of this static class.

#if NETFRAMEWORK

        private static string InternalGetAppDomainAppId()
        {
            return HttpRuntime.AppDomainAppId;
        }
        public static Func<string> GetAppDomainAppId = InternalGetAppDomainAppId;

        private static string InternalGetAppDomainAppVirtualPath()
        {
            return HttpRuntime.AppDomainAppVirtualPath;
        }
        public static Func<string> GetAppDomainAppVirtualPath = InternalGetAppDomainAppVirtualPath;

        private static string InternalGetAppDomainAppPath()
        {
            return HttpRuntime.AppDomainAppPath;
        }
        public static Func<string> GetAppDomainAppPath = InternalGetAppDomainAppPath;

        public static Func<string, System.Configuration.Configuration> OpenWebConfiguration = System.Web.Configuration.WebConfigurationManager.OpenWebConfiguration;
#else
        private static string InternalGetAppDomainName()
        {
            return AppDomain.CurrentDomain.FriendlyName;
        }
        public static Func<string> GetAppDomainName = InternalGetAppDomainName;
#endif

        public static Func<string, bool> FileExists = File.Exists;
        public static Func<string, string> FileReadAllText = File.ReadAllText;

        public static Func<string, string> PathGetDirectoryName = Path.GetDirectoryName;
        public static Func<string, string> GetEnvironmentVar = System.Environment.GetEnvironmentVariable;

        private static string InternalGetNewRelicHome()
        {
            return AgentInstallConfiguration.NewRelicHome;
        }
        public static Func<string> GetNewRelicHome = InternalGetNewRelicHome;

        #endregion Unit test helpers

        /// <summary>
        /// Gets the bootstrap configuration for the agent. The settings in this config does not change over time, and
        /// it is only available after the ConfigurationLoader.Initialize method has been called. If this property is
        /// accessed before the Initialize method has been called, it will return a default bootstrap configuration.
        /// </summary>
        public static IBootstrapConfiguration BootstrapConfig { get; private set; } = BootstrapConfiguration.GetDefault();

        /// <summary>
        /// Reads an application setting from the web configuration associated with the current virtual path,
        /// or from the web site if none is found.
        /// Typically, this will attempt to read the "web.config" or "Web.Config" file for the path.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public static ValueWithProvenance<string> GetWebConfigAppSetting(string key)
        {
            ValueWithProvenance<string> value = null;
#if NETFRAMEWORK
            try
            {
                if (GetAppDomainAppId() != null)
                {
                    string appVirtualPath = GetAppDomainAppVirtualPath();
                    var webConfiguration = OpenWebConfiguration(appVirtualPath);
                    var setting = webConfiguration.AppSettings.Settings[key];
                    if (setting != null)
                    {
                        return new ValueWithProvenance<string>(setting.Value, webConfiguration.FilePath);
                    }
                }
            }
            catch (Exception)
            {
                // Can't log anything here because the logging hasn't been initialized.  Just swallow the exception.
            }
            value = new ValueWithProvenance<string>(System.Web.Configuration.WebConfigurationManager.AppSettings[key],
                "WebConfigurationManager default app settings");
#endif
            return value;
        }

        public static ValueWithProvenance<string> GetConfigSetting(string key)
        {
            ValueWithProvenance<string> value = GetWebConfigAppSetting(key);
#if NETFRAMEWORK
            if (value.Value == null)
            {
                value = new ValueWithProvenance<string>(ConfigurationManager.AppSettings[key],
                    "ConfigurationManager app setting");
            }
#else
            if (value?.Value == null)
            {
                var configMgrStatic = new ConfigurationManagerStatic();
                var configValue = configMgrStatic.GetAppSetting(key);
                if (configValue != null)
                    value = new ValueWithProvenance<string>(configValue, configMgrStatic.AppSettingsFilePath);
            }
#endif
            return value;
        }

        /// <summary>
        /// Returns the agent configuration file name.
        /// </summary>
        /// <returns>The name of the agent configuration file name, such as "newrelic.config".</returns>
        public static string GetAgentConfigFileName()
        {
            var fileName = TryGetAgentConfigFileFromAppConfig()
                ?? TryGetAgentConfigFileFromAppRoot()
                ?? TryGetAgentConfigFileFromExecutionPath()
                ?? TryGetAgentConfigFileFromNewRelicHome()
                ?? TryGetAgentConfigFileFromCurrentDirectory();

            if (fileName != null)
                return fileName;

            throw new Exception(string.Format("Could not find {0} in {1} path, application root, New Relic home directory, or working directory.", NewRelicConfigFileName, Constants.AppSettingsConfigFile));
        }

        private static string TryGetAgentConfigFileFromAppConfig()
        {

#if NETSTANDARD2_0

            try
            {
                var fileName = AppSettingsConfigResolveWhenUsed.GetAppSetting(Constants.AppSettingsConfigFile);
                if (!FileExists(fileName))
                {
                    return null;
                }

                Log.Info("Configuration file found in path pointed to by {0} appSetting: {1}", Constants.AppSettingsConfigFile, fileName);
                return fileName;
            }
            catch (Exception)
            {
                return null;
            }

#else
            try
            {
                var fileName = GetConfigSetting(Constants.AppSettingsConfigFile).Value;
                if (!FileExists(fileName))
                {
                    return null;
                }

                Log.Info("Configuration file found in path pointed to by {0} appSetting of app/web config: {1}", Constants.AppSettingsConfigFile, fileName);
                return fileName;
            }
            catch (Exception)
            {
                return null;
            }
#endif
        }

        private static string TryGetAgentConfigFileFromAppRoot()
        {
#if NETSTANDARD2_0
            try
            {
                var filename = string.Empty;

                var entryAssembly = Assembly.GetEntryAssembly();
                if (entryAssembly != null)
                {
                    var directory = Path.GetDirectoryName(entryAssembly.Location);
                    filename = Path.Combine(directory, NewRelicConfigFileName);
                    if (FileExists(filename))
                    {
                        Log.Info("Configuration file found in app/web root directory: {0}", filename);
                        return filename;
                    }
                }

                var currentDirectory = Directory.GetCurrentDirectory();
                filename = Path.Combine(currentDirectory, NewRelicConfigFileName);
                if (FileExists(filename))
                {
                    Log.Info("Configuration file found in app/web root directory: {0}", filename);
                    return filename;
                }

                return null;
            }
            catch (Exception)
            {
                return null;
            }
#else
            try
            {
                if (GetAppDomainAppVirtualPath() == null) return null;

                var appRoot = GetAppDomainAppPath();
                if (appRoot == null)
                    return null;

                var fileName = Path.Combine(appRoot, NewRelicConfigFileName);
                if (!FileExists(fileName))
                    return null;

                Log.Info("Configuration file found in app/web root directory: {0}", fileName);
                return fileName;
            }
            catch (Exception)
            {
                return null;
            }
#endif
        }

        private static string TryGetAgentConfigFileFromExecutionPath()
        {
            try
            {
                var mainModuleFilePath = _processStatic.GetCurrentProcess().MainModuleFileName;
                var executionPath = PathGetDirectoryName(mainModuleFilePath);
                if (executionPath == null)
                    return null;

                var fileName = Path.Combine(executionPath, NewRelicConfigFileName);
                if (!FileExists(fileName))
                    return null;

                Log.Info("Configuration file found in execution path: {0}", fileName);
                return fileName;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static string TryGetAgentConfigFileFromNewRelicHome()
        {
            try
            {
                var newRelicHome = GetNewRelicHome();
                if (newRelicHome == null)
                    return null;

                var fileName = Path.Combine(newRelicHome, NewRelicConfigFileName);
                if (!FileExists(fileName))
                    return null;

                Log.Info("Configuration file found in New Relic home directory: {0}", fileName);
                return fileName;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static string TryGetAgentConfigFileFromCurrentDirectory()
        {
            try
            {
                if (!FileExists(NewRelicConfigFileName))
                    return null;

                Log.Info("Configuration file found in current working directory: {0}", NewRelicConfigFileName);
                return NewRelicConfigFileName;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static string GetConfigurationFilePath(string homeDirectory)
        {
            var fileName = Path.Combine(homeDirectory, NewRelicConfigFileName);
            if (!FileExists(fileName))
            {
                throw new Exception(string.Format("Could not find the config file in the new relic home directory. Check New Relic home directory for {0}.", NewRelicConfigFileName));
            }
            return fileName;
        }

        /// <summary>
        /// Initialize and return a BootstrapConfig, from a fixed, well-known file name.
        /// </summary>
        /// <returns></returns>
        public static configuration Initialize(bool publishDeserializedEvent = true)
        {
            var fileName = string.Empty;
            try
            {
                fileName = GetAgentConfigFileName();
                if (!FileExists(fileName))
                {
                    throw new ConfigurationLoaderException(string.Format("The New Relic Agent configuration file does not exist: {0}", fileName));
                }
                var config = Initialize(fileName, publishDeserializedEvent);
                BootstrapConfig = new BootstrapConfiguration(config, fileName);
                return config;
            }
            catch (FileNotFoundException ex)
            {
                throw HandleConfigError(string.Format("Unable to find the New Relic Agent configuration file {0}", fileName), ex);
            }
            catch (UnauthorizedAccessException ex)
            {
                throw HandleConfigError(string.Format("Unable to access the New Relic Agent configuration file {0}", fileName), ex);
            }
            catch (Exception ex)
            {
                throw HandleConfigError(string.Format("An error occurred reading the New Relic Agent configuration file {0} - {1}", fileName, ex.Message), ex);
            }
        }

        private static Exception HandleConfigError(string message, Exception originalException)
        {
            Log.Error(message);
            return new ConfigurationLoaderException(message, originalException);
        }

        /// <summary>
        /// Initialize the configuration by reading xml contained in the file named fileName.
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="publishDeserializedEvent"></param>
        /// <exception cref="">System.UnauthorizedAccessException</exception>
        /// <returns>The configuration.</returns>
        public static configuration Initialize(string fileName, bool publishDeserializedEvent = true)
        {
            using (StreamReader stream = new StreamReader(fileName))
            {
                configuration config = InitializeFromXml(stream.ReadToEnd(), GetConfigSchemaContents, fileName, publishDeserializedEvent);
                return config;
            }
        }

        private static void ValidationEventHandler(object sender, ValidationEventArgs e)
        {
            switch (e.Severity)
            {
                case XmlSeverityType.Error:
                    Log.Error(e.Exception, "An error occurred parsing {0}", NewRelicConfigFileName);
                    break;
                case XmlSeverityType.Warning:
                    Log.Warn(e.Exception, "{0} warning", NewRelicConfigFileName);
                    break;
            }

        }

        /// <summary>
        /// Initialize the configuration by reading xml from a string.
        /// </summary>
        /// <param name="configXml"></param>
        /// <param name="configSchemaSource">A method that returns a string containing the config schema (xsd).</param>
        /// <param name="provenance">The file name or other user-friendly locus where the xml came from.</param>
        /// <param name="publishDeserializedEvent"></param>
        /// <returns>The configuration.</returns>
        public static configuration InitializeFromXml(string configXml, Func<string> configSchemaSource, string provenance = "unknown", bool publishDeserializedEvent = true)
        {
            configuration config;

            // deserialize the xml, making sure to pass in the root attribute to avoid fusion log failures.
            XmlRootAttribute root = new XmlRootAttribute();
            root.ElementName = "configuration";
            root.Namespace = "urn:newrelic-config";
            XmlSerializer serializer = new XmlSerializer(typeof(configuration), root);

            using (TextReader reader = new StringReader(configXml))
            {
                config = serializer.Deserialize(reader) as configuration;
                if (config == null)
                    throw new InvalidDataException(string.Format("Unable to deserialize the provided xml: {0}", configXml));
            }

            // Validate the config xml with the supplied schema.  Note that any validation failures do not prevent
            // agent initialization and only generate a warning log message.
            try
            {
                ValidateConfigXmlWithSchema(configXml, configSchemaSource());
            }
            catch (Exception ex)
            {
                Log.Warn(ex, "An unknown error occurred when performing XML schema validation on config file {0}", NewRelicConfigFileName);
            }

            if (publishDeserializedEvent)
                PublishDeserializedEvent(config);

            return config;
        }
        public static void PublishDeserializedEvent(configuration config)
        {
            EventBus<ConfigurationDeserializedEvent>.Publish(new ConfigurationDeserializedEvent(config));
        }

        /// <summary>
        /// Used ONLY FOR TESTING!
        /// </summary>
        public static void UseBootstrapConfigurationForTesting(IBootstrapConfiguration bootstrapConfiguration)
        {
            BootstrapConfig = bootstrapConfiguration;
        }


        private static void ValidateConfigXmlWithSchema(string configXml, string schemaXml)
        {
            // xml validation with schema
            ValidationEventHandler eventHandler = new ValidationEventHandler(ValidationEventHandler);

            using (var configStringReader = new StringReader(schemaXml))
            using (var schemaReader = new XmlTextReader(configStringReader))
            using (var stringReader = new StringReader(configXml))
            using (var xmlReader = XmlReader.Create(stringReader))
            {
                XmlDocument document = new XmlDocument();

                // validate the xml
                try
                {

                    document.Load(xmlReader);
                    RemoveApdexAttribute(document);
                    RemoveSslAttribute(document);

                    document.Schemas.Add(XmlSchema.Read(schemaReader, eventHandler));
                    document.Validate(eventHandler);
                }
                catch (Exception ex)
                {
                    Log.Warn(ex, "An error occurred parsing {0}", NewRelicConfigFileName);
                }
            }
        }

        private static string GetConfigSchemaContents()
        {
            var configSchemaContents = string.Empty;

            try
            {
                var home = AgentInstallConfiguration.NewRelicHome;
                var xsdFile = Path.Combine(home, "newrelic.xsd");
                configSchemaContents = FileReadAllText(xsdFile);
            }
            catch (Exception ex)
            {
                Log.Warn(ex, "An error occurred reading config file schema");
            }

            return configSchemaContents;
        }

        /// <summary>
        /// Remove old apdexT nodes from the given document so they don't throw a validation exception.
        /// </summary>
        /// <param name="document">The handle on the document.</param>
        private static void RemoveApdexAttribute(XmlDocument document)
        {
            try
            {
                XmlNamespaceManager ns = new XmlNamespaceManager(document.NameTable);
                ns.AddNamespace("nr", "urn:newrelic-config");
                XmlNode node = document.SelectSingleNode("//nr:configuration/nr:application", ns);
                if (node != null)
                {
                    if (node.Attributes.RemoveNamedItem("apdexT") != null)
                    {
                        Log.Finest("Removed apdexT from configuration.");
                    }
                }
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// Removes deprecated attribute to avoid validation errors. 
        /// The generated error isn't super clear and could risk causing support tickets.
        /// </summary>
        /// <param name="document"></param>
        private static void RemoveSslAttribute(XmlDocument document)
        {
            try
            {
                var ns = new XmlNamespaceManager(document.NameTable);
                ns.AddNamespace("nr", "urn:newrelic-config");
                var node = document.SelectSingleNode("//nr:configuration/nr:service", ns);

                var sslAttribute = node?.Attributes?["ssl"];
                if (sslAttribute != null)
                {
                    Log.Warn("'ssl' is no longer a configurable service attribute and cannot be disabled. Please remove from {0}.", NewRelicConfigFileName);

                    node.Attributes.Remove(sslAttribute);
                }
            }
            catch (Exception)
            {
                // ignored
            }
        }
    }

    /// <summary>
    /// Thrown when there is some problem loading the configuration.
    /// </summary>
    public class ConfigurationLoaderException : Exception
    {
        public ConfigurationLoaderException(string message)
            : base(message)
        {
        }

        public ConfigurationLoaderException(string message, Exception original)
            : base(message, original)
        {
        }
    }

    /// <summary>
    /// This class contains helper methods that allow resusing logic from the non-static DefaultConfiguration
    /// class from within static contexts like the bootstrap process.
    /// </summary>
    public static class ConfigLoaderHelpers
    {
        // This field exists only for testing purposes. It allows bootstrap logic and regular
        // configuration logic to use the same environment variable logic.
        public static IEnvironment EnvironmentVariableProxy = new SharedInterfaces.Environment();

        public static string GetEnvironmentVar(params string[] environmentVariableNames)
        {
            var result = (environmentVariableNames ?? Enumerable.Empty<string>())
                .Select(EnvironmentVariableProxy.GetEnvironmentVariable)
                .FirstOrDefault(value => value != null);

            // needs to return null instead of empty string
            return result == string.Empty ? null : result;
        }

        public static string GetOverride(string name, string fallback)
        {
            return DefaultConfiguration.EnvironmentOverrides(EnvironmentVariableProxy, fallback, name);
        }
        public static int GetOverride(string name, int fallback)
        {
            return DefaultConfiguration.EnvironmentOverrides(EnvironmentVariableProxy, fallback, name).Value;
        }

        public static bool GetOverride(string name, bool fallback)
        {
            return DefaultConfiguration.EnvironmentOverrides(EnvironmentVariableProxy, fallback, name);
        }

        public static bool TryToBoolean(this string val, out bool boolVal)
        {
            boolVal = false;

            if (string.IsNullOrEmpty(val))
            {
                return false;
            }

            val = val.ToLower();

            if (bool.TryParse(val, out boolVal))
            {
                return true;
            }

            switch (val)
            {
                case "0":
                    boolVal = false;
                    return true;
                case "1":
                    boolVal = true;
                    return true;
                default:
                    return false;
            }
        }

        public static bool GetLoggingEnabledValue(configurationLog localLogConfiguration)
        {
            return DefaultConfiguration.GetLoggingEnabledValue(EnvironmentVariableProxy, localLogConfiguration);
        }

        public static string GetLoggingLevelValue(configurationLog localLogConfiguration, bool isLoggingEnabled)
        {
            return DefaultConfiguration.GetLoggingLevelValue(EnvironmentVariableProxy, localLogConfiguration, isLoggingEnabled);
        }
    }

}
