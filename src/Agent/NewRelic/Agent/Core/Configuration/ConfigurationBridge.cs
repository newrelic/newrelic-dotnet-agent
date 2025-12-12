// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#if NETSTANDARD2_0
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using NewRelic.Agent.Extensions.Logging;
using NewRelic.Agent.Core.Utilities;

namespace NewRelic.Agent.Core.Configuration
{
    /// <summary>
    /// Internal bridging logic for accessing the application's Microsoft.Extensions.Configuration system.
    /// This class is internal to prevent IoC containers from discovering and registering it.
    /// </summary>
    internal static class ConfigurationBridge
    {
        private static readonly ConcurrentDictionary<string, Type> _cachedTypes = new();
        private static readonly object _initializationLock = new object();
        private static volatile bool _initialized = false;
        private static volatile bool _bridgeAvailable = false;
        private static Func<string, string> _getConfigurationValueDelegate = null;

        /// <summary>
        /// Initializes the configuration bridge. Attempts to locate and bridge to the application's Microsoft.Extensions.Configuration system.
        /// If the bridge cannot be established, falls back to ILRepacked configuration logic.
        /// </summary>
        public static void Initialize()
        {
            if (_initialized)
                return;

            lock (_initializationLock)
            {
                if (_initialized)
                    return;

                try
                {
                    _bridgeAvailable = TryInitializeBridge();
                    Log.Debug($"ConfigurationBridge initialization complete. Bridge available: {_bridgeAvailable}");
                }
                catch (Exception ex)
                {
                    Log.Debug(ex, "ConfigurationBridge initialization failed. Falling back to ILRepacked configuration.");
                    _bridgeAvailable = false;
                }
                finally
                {
                    _initialized = true;
                }
            }
        }

        /// <summary>
        /// Retrieves the value for the specified configuration key from the application's configuration system.
        /// Falls back to ILRepacked configuration if the bridge is unavailable or the key is not found.
        /// </summary>
        /// <param name="key">The configuration key to retrieve.</param>
        /// <returns>The configuration value, or null if not found.</returns>
        public static string GetAppSetting(string key)
        {
            // Fast path: null or empty key check before initialization
            if (string.IsNullOrWhiteSpace(key))
                return null;

            Initialize();

            if (!_bridgeAvailable || _getConfigurationValueDelegate == null)
            {
                // Fall back to ILRepacked configuration
                return AppSettingsConfigResolveWhenUsed.GetAppSetting(key);
            }

            try
            {
                var value = _getConfigurationValueDelegate(key);

                // If value is found in application config, use it
                if (!string.IsNullOrWhiteSpace(value))
                {
                    if (Log.IsDebugEnabled)
                    {
                        var logValue = ShouldObfuscateKey(key)
                            ? Strings.ObfuscateLicenseKey(value)
                            : value;
                        Log.Debug($"ConfigurationBridge: Retrieved '{key}={logValue}' from application configuration.");
                    }
                    return value;
                }

                // If not found in application config, try ILRepacked as fallback
                if (Log.IsDebugEnabled)
                {
                    Log.Debug($"ConfigurationBridge: '{key}' not found in application configuration, falling back to ILRepacked configuration.");
                }

                return AppSettingsConfigResolveWhenUsed.GetAppSetting(key);
            }
            catch (Exception ex)
            {
                Log.Debug(ex, $"ConfigurationBridge: Error accessing application configuration for key '{key}', falling back to ILRepacked configuration.");
                return AppSettingsConfigResolveWhenUsed.GetAppSetting(key);
            }
        }

        /// <summary>
        /// Gets the file path to the application's configuration file (e.g., appsettings.json).
        /// Falls back to the ILRepacked configuration file path if the bridge is unavailable or the file is not found.
        /// </summary>
        /// <returns>The configuration file path.</returns>
        public static string GetAppSettingsFilePath()
        {
            Initialize();

            if (!_bridgeAvailable)
            {
                return AppSettingsConfigResolveWhenUsed.AppSettingsFilePath;
            }

            try
            {
                // Try to determine application's config file path
                var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
                var appSettingsPath = Path.Combine(baseDirectory, "appsettings.json");

                if (File.Exists(appSettingsPath))
                {
                    return appSettingsPath;
                }

                return AppSettingsConfigResolveWhenUsed.AppSettingsFilePath;
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "ConfigurationBridge: Error determining application configuration file path.");
                return AppSettingsConfigResolveWhenUsed.AppSettingsFilePath;
            }
        }

        private static bool ShouldObfuscateKey(string key)
        {
            return string.Equals(key, Constants.AppSettingsLicenseKey, StringComparison.Ordinal);
        }
        
        private static bool TryInitializeBridge()
        {
            // Try to find the application's IConfiguration type
            var configurationType = FindApplicationConfigurationType();
            if (configurationType == null)
            {
                Log.Debug("ConfigurationBridge: Microsoft.Extensions.Configuration.IConfiguration not found in application assemblies.");
                return false;
            }

            // Try to find an existing IConfiguration instance in the application
            var configurationInstance = FindApplicationConfigurationInstance(configurationType);
            if (configurationInstance == null)
            {
                Log.Debug("ConfigurationBridge: No application IConfiguration instance found.");
                return false;
            }

            // Try to create a delegate for accessing configuration values
            var getValueDelegate = CreateGetValueDelegate(configurationType, configurationInstance);
            if (getValueDelegate == null)
            {
                Log.Debug("ConfigurationBridge: Failed to create configuration value accessor delegate.");
                return false;
            }

            // Store only the delegate, not the configuration instance
            _getConfigurationValueDelegate = getValueDelegate;

            Log.Debug("ConfigurationBridge: Successfully bridged to application configuration system.");
            return true;
        }

        private static Type FindApplicationConfigurationType()
        {
            // Try to find the application's IConfiguration type in loaded assemblies
            return _cachedTypes.GetOrAdd("IConfiguration", _ =>
            {
                try
                {
                    // Look for Microsoft.Extensions.Configuration.IConfiguration in loaded assemblies
                    // Exclude our own ILRepacked assembly and system assemblies for better performance
                    var assemblies = AppDomain.CurrentDomain.GetAssemblies()
                        .Where(a => !a.FullName.Contains("NewRelic") &&
                                   !a.FullName.Contains("ILRepacked") &&
                                   !a.Location.Contains("NewRelic") &&
                                   !a.GlobalAssemblyCache && // Exclude GAC assemblies
                                   !string.IsNullOrEmpty(a.Location)) // Exclude dynamic assemblies
                        .ToArray();

                    foreach (var assembly in assemblies)
                    {
                        try
                        {
                            // Quick check: only scan assemblies that likely contain Microsoft.Extensions.Configuration
                            if (!assembly.FullName.Contains("Microsoft.Extensions") &&
                                !assembly.FullName.Contains("Extensions.Configuration") &&
                                !assembly.GetReferencedAssemblies().Any(r => r.Name.Contains("Microsoft.Extensions")))
                            {
                                continue;
                            }

                            var configType = assembly.GetTypes()
                                .FirstOrDefault(t => t.FullName == "Microsoft.Extensions.Configuration.IConfiguration");

                            if (configType != null)
                            {
                                Log.Debug($"ConfigurationBridge: Found IConfiguration type in assembly: {assembly.FullName}");
                                return configType;
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.Debug(ex, $"ConfigurationBridge: Error examining assembly {assembly.FullName}");
                        }
                    }

                    return null;
                }
                catch (Exception ex)
                {
                    Log.Debug(ex, "ConfigurationBridge: Error finding application configuration type.");
                    return null;
                }
            });
        }

        private static object FindApplicationConfigurationInstance(Type configurationType)
        {
            try
            {
                // Try to find an IConfiguration instance via ServiceProvider
                var serviceProviderInstance = FindServiceProvider();
                if (serviceProviderInstance != null)
                {
                    var configInstance = GetServiceFromProvider(serviceProviderInstance, configurationType);
                    if (configInstance != null)
                    {
                        Log.Debug("ConfigurationBridge: Found IConfiguration instance via ServiceProvider.");
                        return configInstance;
                    }
                }

                // Try to find a static IConfiguration instance in loaded types
                var staticConfigInstance = FindStaticConfigurationInstance(configurationType);
                if (staticConfigInstance != null)
                {
                    Log.Debug("ConfigurationBridge: Found IConfiguration instance via static field search.");
                    return staticConfigInstance;
                }

                Log.Debug("ConfigurationBridge: No application configuration instance found.");
                return null;
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "ConfigurationBridge: Error finding application configuration instance.");
                return null;
            }
        }

        // Fix for S3011: Ensure accessibility bypass is safe  
        private static object FindServiceProvider()
        {
            // Try to find a static IServiceProvider instance in loaded assemblies
            try
            {
                var assemblies = AppDomain.CurrentDomain.GetAssemblies()
                    .Where(a => !a.FullName.Contains("NewRelic") && !a.FullName.Contains("ILRepacked"))
                    .ToArray();

                foreach (var assembly in assemblies)
                {
                    try
                    {
                        var serviceProviderType = assembly.GetTypes()
                            .FirstOrDefault(t => t.GetInterfaces().Any(i => i.Name == "IServiceProvider"));

                        if (serviceProviderType != null)
                        {
                            // Try to find static fields of type IServiceProvider
                            var staticFields = serviceProviderType.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                                .Where(f => f.FieldType.GetInterfaces().Any(i => i.Name == "IServiceProvider"))
                                .ToArray();

                            foreach (var field in staticFields)
                            {
                                var instance = field.GetValue(null);
                                if (instance != null)
                                {
                                    return instance;
                                }
                            }

                            // Try to find static properties of type IServiceProvider
                            var staticProperties = serviceProviderType.GetProperties(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                                .Where(p => p.PropertyType.GetInterfaces().Any(i => i.Name == "IServiceProvider"))
                                .ToArray();

                            foreach (var property in staticProperties)
                            {
                                try
                                {
                                    var instance = property.GetValue(null);
                                    if (instance != null)
                                    {
                                        return instance;
                                    }
                                }
                                catch
                                {
                                    // Ignore property access errors  
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Debug(ex, $"ConfigurationBridge: Error examining assembly {assembly.FullName} for ServiceProvider.");
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "ConfigurationBridge: Error finding ServiceProvider.");
                return null;
            }
        }

        private static object GetServiceFromProvider(object serviceProvider, Type serviceType)
        {
            // Try to get a service from the IServiceProvider instance
            try
            {
                var getServiceMethod = serviceProvider.GetType().GetMethod("GetService", new[] { typeof(Type) });
                if (getServiceMethod != null)
                {
                    return getServiceMethod.Invoke(serviceProvider, new object[] { serviceType });
                }

                return null;
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "ConfigurationBridge: Error getting service from ServiceProvider.");
                return null;
            }
        }

        private static object FindStaticConfigurationInstance(Type configurationType)
        {
            // Try to find a static IConfiguration instance in loaded types
            try
            {
                // Fix for IDE0300: Simplify collection initialization  
                var assemblies = AppDomain.CurrentDomain.GetAssemblies()
                   .Where(a => !a.FullName.Contains("NewRelic") && !a.FullName.Contains("ILRepacked"))
                   .ToArray();

                foreach (var assembly in assemblies)
                {
                    try
                    {
                        var types = assembly.GetTypes()
                            .Where(t => t.IsClass && !t.IsAbstract) // Only scan concrete classes
                            .ToArray();

                        foreach (var type in types)
                        {
                            try
                            {
                                // Try to find static fields of type IConfiguration
                                var staticFields = type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                                    .Where(f => configurationType.IsAssignableFrom(f.FieldType))
                                    .ToArray();

                                foreach (var field in staticFields)
                                {
                                    var instance = field.GetValue(null);
                                    if (instance != null)
                                    {
                                        Log.Debug($"ConfigurationBridge: Found static configuration field '{field.Name}' in type '{type.FullName}'");
                                        return instance;
                                    }
                                }

                                // Try to find static properties of type IConfiguration
                                var staticProperties = type.GetProperties(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                                    .Where(p => configurationType.IsAssignableFrom(p.PropertyType) && p.CanRead)
                                    .ToArray();

                                foreach (var property in staticProperties)
                                {
                                    try
                                    {
                                        var instance = property.GetValue(null);
                                        if (instance != null)
                                        {
                                            Log.Debug($"ConfigurationBridge: Found static configuration property '{property.Name}' in type '{type.FullName}'");
                                            return instance;
                                        }
                                    }
                                    catch
                                    {
                                        // Ignore property access errors
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                Log.Debug(ex, $"ConfigurationBridge: Error examining type {type.FullName}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Debug(ex, $"ConfigurationBridge: Error examining assembly {assembly.FullName} for static configuration.");
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "ConfigurationBridge: Error finding static configuration instance.");
                return null;
            }
        }

        private static Func<string, string> CreateGetValueDelegate(Type configurationType, object configurationInstance)
        {
            // Try to create a delegate for accessing configuration values via the indexer
            try
            {
                // Look for the indexer property: string this[string key] { get; }
                var indexerProperty = configurationType.GetProperties()
                    .FirstOrDefault(p => p.GetIndexParameters().Length == 1 &&
                                        p.GetIndexParameters()[0].ParameterType == typeof(string) &&
                                        p.PropertyType == typeof(string));

                if (indexerProperty == null)
                {
                    Log.Debug("ConfigurationBridge: IConfiguration indexer property not found.");
                    return null;
                }

                // Create a compiled expression to access the indexer: (key) => configuration[key]
                var keyParameter = Expression.Parameter(typeof(string), "key");
                var configurationConstant = Expression.Constant(configurationInstance, configurationType);
                var indexerAccess = Expression.Property(configurationConstant, indexerProperty, keyParameter);

                var lambda = Expression.Lambda<Func<string, string>>(indexerAccess, keyParameter);
                var compiledDelegate = lambda.Compile();

                Log.Debug("ConfigurationBridge: Successfully created configuration value accessor delegate.");
                return compiledDelegate;
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "ConfigurationBridge: Error creating configuration value accessor delegate.");
                return null;
            }
        }
    }
}
#endif
