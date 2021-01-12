// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Core.Events;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Core;
using NewRelic.Core.Logging;
using NewRelic.SystemInterfaces;
using System;
using System.Configuration;
using System.IO;
using System.Web;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
#if NETSTANDARD2_0
using System.Reflection;
using NewRelic.Agent.Core.Configuration;
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

        /// <summary>
        /// Reads an application setting from the web configuration associated with the current virtual path,
        /// or from the web site if none is found.
        /// Typically, this will attempt to read the "web.config" or "Web.Config" file for the path.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public static ValueWithProvenance<string> GetWebConfigAppSetting(string key)
        {
#if NETSTANDARD2_0
			return null;
#else
            try
            {
                if (HttpRuntime.AppDomainAppId != null)
                {
                    string appVirtualPath = HttpRuntime.AppDomainAppVirtualPath;
                    var webConfiguration = System.Web.Configuration.WebConfigurationManager.OpenWebConfiguration(appVirtualPath);
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
            return new ValueWithProvenance<string>(System.Web.Configuration.WebConfigurationManager.AppSettings[key],
                "WebConfigurationManager default app settings");
#endif
        }

        public static ValueWithProvenance<string> GetConfigSetting(string key)
        {
            ValueWithProvenance<string> value = GetWebConfigAppSetting(key);
#if NET45
			if (value.Value == null)
			{
				value = new ValueWithProvenance<string>(ConfigurationManager.AppSettings[key],
					"ConfigurationManager app setting");
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

            throw new Exception(string.Format("Could not find {0} in NewRelic.ConfigFile path, application root, New Relic home directory, or working directory.", NewRelicConfigFileName));
        }

        private static string TryGetAgentConfigFileFromAppConfig()
        {

#if NETSTANDARD2_0

			try
			{
				var fileName = AppSettingsConfigResolveWhenUsed.GetAppSetting("NewRelic.ConfigFile");
				if (!File.Exists(fileName))
				{
					return null;
				}

				Log.InfoFormat("Configuration file found in path pointed to by NewRelic.ConfigFile appSetting: {0}", fileName);
				return fileName;
			}
			catch (Exception)
			{
				return null;
			}

#else
            try
            {
                var fileName = GetConfigSetting("NewRelic.ConfigFile").Value;
                if (!File.Exists(fileName))
                {
                    return null;
                }

                Log.InfoFormat("Configuration file found in path pointed to by NewRelic.ConfigFile appSetting of app/web config: {0}", fileName);
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
					if (File.Exists(filename))
					{
						Log.InfoFormat("Configuration file found in app/web root directory: {0}", filename);
						return filename;
					}
				}

				var currentDirectory = Directory.GetCurrentDirectory();
				filename = Path.Combine(currentDirectory, NewRelicConfigFileName);
				if (File.Exists(filename))
				{
					Log.InfoFormat("Configuration file found in app/web root directory: {0}", filename);
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
                if (HttpRuntime.AppDomainAppVirtualPath == null) return null;

                var appRoot = HttpRuntime.AppDomainAppPath;
                if (appRoot == null)
                    return null;

                var fileName = Path.Combine(appRoot, NewRelicConfigFileName);
                if (!File.Exists(fileName))
                    return null;

                Log.InfoFormat("Configuration file found in app/web root directory: {0}", fileName);
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
                var executionPath = Path.GetDirectoryName(mainModuleFilePath);
                if (executionPath == null)
                    return null;

                var fileName = Path.Combine(executionPath, NewRelicConfigFileName);
                if (!File.Exists(fileName))
                    return null;

                Log.InfoFormat("Configuration file found in execution path: {0}", fileName);
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
                var newRelicHome = AgentInstallConfiguration.NewRelicHome;
                if (newRelicHome == null)
                    return null;

                var fileName = Path.Combine(newRelicHome, NewRelicConfigFileName);
                if (!File.Exists(fileName))
                    return null;

                Log.InfoFormat("Configuration file found in New Relic home directory: {0}", fileName);
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
                if (!File.Exists(NewRelicConfigFileName))
                    return null;

                Log.InfoFormat("Configuration file found in current working directory: {0}", NewRelicConfigFileName);
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
            if (!File.Exists(fileName))
            {
                throw new Exception(string.Format("Could not find the config file in the new relic home directory. Check New Relic home directory for {0}.", NewRelicConfigFileName));
            }
            return fileName;
        }

        /// <summary>
        /// Initialize and return a BootstrapConfig, from a fixed, well-known file name.
        /// </summary>
        /// <returns></returns>
        public static configuration Initialize()
        {
            var fileName = string.Empty;
            try
            {
                fileName = GetAgentConfigFileName();
                if (!File.Exists(fileName))
                {
                    throw new ConfigurationLoaderException(string.Format("The New Relic Agent configuration file does not exist: {0}", fileName));
                }
                return Initialize(fileName);
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
        /// <exception cref="">System.UnauthorizedAccessException</exception>
        /// <returns>The configuration.</returns>
        public static configuration Initialize(string fileName)
        {
            using (StreamReader stream = new StreamReader(fileName))
            {
                configuration config = InitializeFromXml(stream.ReadToEnd(), fileName);
                config.ConfigurationFileName = fileName;
                return config;
            }
        }

        static void ValidationEventHandler(object sender, ValidationEventArgs e)
        {
            switch (e.Severity)
            {
                case XmlSeverityType.Error:
                    Log.ErrorFormat("An error occurred parsing {0} - {1}", NewRelicConfigFileName, e.Message);
                    break;
                case XmlSeverityType.Warning:
                    Log.WarnFormat("{0} warning - {1}", NewRelicConfigFileName, e.Message);
                    break;
            }

        }

        /// <summary>
        /// Initialize the configuration by reading xml from a string.
        /// </summary>
        /// <param name="xml"></param>
        /// <param name="provenance">The file name or other user-friendly locus where the xml came from.</param>
        /// <returns>The configuration.</returns>
        public static configuration InitializeFromXml(string xml, string provenance = "unknown")
        {
            configuration config;

            // deserialize the xml, making sure to pass in the root attribute to avoid fusion log failures.
            XmlRootAttribute root = new XmlRootAttribute();
            root.ElementName = "configuration";
            root.Namespace = "urn:newrelic-config";
            XmlSerializer serializer = new XmlSerializer(typeof(configuration), root);

            using (TextReader reader = new StringReader(xml))
            {
                config = serializer.Deserialize(reader) as configuration;
                if (config == null)
                    throw new InvalidDataException(string.Format("Unable to deserialize the provided xml: {0}", xml));
                config.Initialize(xml, provenance);
            }

            // xml validation with schema file
            ValidationEventHandler eventHandler = new ValidationEventHandler(ValidationEventHandler);

            using (var configStringReader = new StringReader(GetConfigSchemaContents()))
            using (var schemaReader = new XmlTextReader(configStringReader))
            using (var stringReader = new StringReader(xml))
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
                    Log.WarnFormat("An error occurred parsing {0} - {1}", NewRelicConfigFileName, ex.Message);
                }
            }

            EventBus<ConfigurationDeserializedEvent>.Publish(new ConfigurationDeserializedEvent(config));

            return config;
        }

        private static string GetConfigSchemaContents()
        {
#if NET45
			return Properties.Resources.Configuration;
#else
            var home = AgentInstallConfiguration.NewRelicHome;
            var xsdFile = Path.Combine(home, "newrelic.xsd");
            return File.ReadAllText(xsdFile);
#endif
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
                    Log.WarnFormat("'ssl' is no longer a configurable service attribute and cannot be disabled. Please remove from {0}.", NewRelicConfigFileName);

                    node.Attributes.Remove(sslAttribute);
                }
            }
            catch (Exception)
            {
                // ignored
            }
        }
    }

    public partial class configurationLog : ILogConfig
    {
        private static readonly IProcessStatic _processStatic = new ProcessStatic();

        [XmlIgnore()]
        public string LogLevel
        {
            get
            {
                return this.level.ToUpper();
            }
        }

        public string GetFullLogFileName()
        {
            System.Text.StringBuilder fileName = new System.Text.StringBuilder();
            string logDirectory = directory;
            if (logDirectory == null)
            {
                if (AgentInstallConfiguration.NewRelicHome != null)
                {
                    fileName.Append(AgentInstallConfiguration.NewRelicHome);
                    if (!fileName.ToString().EndsWith(Path.DirectorySeparatorChar.ToString()))
                        fileName.Append(Path.DirectorySeparatorChar);
                    fileName.Append("logs").Append(Path.DirectorySeparatorChar);
                }
            }
            else
            {
                fileName.Append(logDirectory);
            }
            if (!fileName.ToString().EndsWith(Path.DirectorySeparatorChar.ToString()))
                fileName.Append(Path.DirectorySeparatorChar);

            fileName.Append(GetLogFileName());
            return fileName.ToString();
        }

        private string GetLogFileName()
        {
            string name = System.Environment.GetEnvironmentVariable("NEW_RELIC_LOG");
            if (name != null)
            {
                return Strings.SafeFileName(name);
            }

            name = fileName;
            if (name != null)
            {
                return Strings.SafeFileName(name);
            }

#if NETSTANDARD2_0
			try
			{
				name = AppDomain.CurrentDomain.FriendlyName;
			}
			catch (Exception)
			{
				name = _processStatic.GetCurrentProcess().ProcessName;
			}
#else
            if (HttpRuntime.AppDomainAppId != null)
            {
                name = HttpRuntime.AppDomainAppId.ToString();
            }
            else
            {
                name = _processStatic.GetCurrentProcess().ProcessName;
            }
#endif

            return "newrelic_agent_" + Strings.SafeFileName(name) + ".log";
        }

        public bool FileLockingModelSpecified
        {
            get
            {
                return fileLockingModelSpecified;
            }
        }

        public configurationLogFileLockingModel FileLockingModel
        {
            get
            {
                return fileLockingModel;
            }
        }

        public bool Console
        {
            get
            {
                return console;
            }
        }

        public bool IsAuditLogEnabled
        {
            get
            {
                return auditLog;
            }
        }

    }

    // The configuration class is partial.  Part of it is implemented here,
    // and another part is autogenerated from Configuration.xsd into Configuration.cs.
    // Beware the tarpit surrounding the case of property names, as different cases reflect
    // different origins of the property names.
    // Property names such as "agentEnabled" come to us from Configuration.xsd via Configuration.cs.
    // Property names such as "AgentEnabled" are added in here or inherited from BootstrapConfig.
    public partial class configuration : IBootstrapConfig
    {
        public string Xml { get; set; }

        [XmlIgnore]
        public string ConfigurationFileName { get; set; }

        public configuration Initialize(string xml, string provenance)
        {
            Xml = xml;

            if (log == null)
                log = new configurationLog();

            try
            {
                var enabledProvenance = ConfigurationLoader.GetConfigSetting("NewRelic.AgentEnabled");
                if (enabledProvenance != null && enabledProvenance.Value != null && bool.Parse(enabledProvenance.Value) == false)
                {
                    agentEnabled = false;
                    AgentEnabledAt = enabledProvenance.Provenance;
                }
            }
            catch
            {
                Log.Error("Failed to read NewRelic.AgentEnabled from local config.");
            }

            return this;
        }

        [XmlIgnore]
        public string AgentEnabledAt { get; private set; }

        [XmlIgnore]
        public ILogConfig LogConfig { get { return log; } }
    }

    /// <summary>
    /// Thrown when there is soime problem loading the configuration.
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

}
