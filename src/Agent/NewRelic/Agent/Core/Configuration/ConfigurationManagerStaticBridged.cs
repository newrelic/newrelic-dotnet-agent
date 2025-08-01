// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NewRelic.Agent.Extensions.Logging;

#if NETSTANDARD2_0
using Microsoft.Extensions.Configuration;
using System.IO;
using System.Reflection;
using NewRelic.Agent.Core.Config;
#endif

namespace NewRelic.Agent.Core.Configuration
{
    /// <summary>
    /// Bridged implementation of IConfigurationManagerStatic that uses reflection to access 
    /// the application's actual configuration system instead of the ILRepacked one.
    /// </summary>
    public class ConfigurationManagerStaticBridged : IConfigurationManagerStatic
    {
        private bool localConfigChecksDisabled;

#if NETSTANDARD2_0
        // Instance-based detection with lazy initialization for optimal performance
        private bool? _isILRepackIsolated = null;
        private readonly object _detectionLock = new object();

        private bool IsILRepackIsolated
        {
            get
            {
                if (_isILRepackIsolated.HasValue)
                    return _isILRepackIsolated.Value;

                lock (_detectionLock)
                {
                    if (_isILRepackIsolated.HasValue)
                        return _isILRepackIsolated.Value;

                    _isILRepackIsolated = DetectILRepackIsolation();
                    return _isILRepackIsolated.Value;
                }
            }
        }

        private bool DetectILRepackIsolation()
    {
    try
    {
        // Check if the specific type we need exists
        var configType = Type.GetType("NewRelic.Agent.Core.Config.AppSettingsConfigResolveWhenUsed, NewRelic.Agent.Core", 
                                     throwOnError: false);
        
        if (configType == null)
        {
            Log.Debug("ILRepack isolation detected: AppSettingsConfigResolveWhenUsed type not found");
            return true;
        }

        // Simple method existence check
        var getAppSettingMethod = configType.GetMethod("GetAppSetting", BindingFlags.Public | BindingFlags.Static);
        
        if (getAppSettingMethod == null)
        {
            Log.Debug("ILRepack isolation detected: GetAppSetting method not found");
            return true;
        }

        Log.Debug("ILRepack isolation not detected: All required types/methods available");
        return false;
    }
    catch (Exception ex)
    {
        Log.Warn(ex, "Unexpected error during ILRepack detection, assuming isolated");
        return true;
    }
}
#endif

        public string AppSettingsFilePath
        {
            get
            {
                try
                {
                    if (!localConfigChecksDisabled)
                    {
#if NETFRAMEWORK
                        return AppDomain.CurrentDomain.SetupInformation.ConfigurationFile;
#else
                        return ConfigurationBridge.GetAppSettingsFilePath();
#endif
                    }
                }
                catch (Exception ex)
                {
                    Log.Debug(ex, "ConfigurationManagerStaticBridged: Error getting AppSettingsFilePath.");
                }

                return null;
            }
        }

        public string GetAppSetting(string key)
        {
            if (localConfigChecksDisabled || key == null)
                return null;

            try
            {
#if NETFRAMEWORK
                return System.Configuration.ConfigurationManager.AppSettings.Get(key);
#else
                //Instance-based detection with optimal routing
                return IsILRepackIsolated
                    ? ConfigurationBridge.GetAppSetting(key)
                    : AppSettingsConfigResolveWhenUsed.GetAppSetting(key);
#endif
            }
            catch (Exception ex)
            {
#if NETFRAMEWORK
                const string framework = "System.Configuration.ConfigurationManager.AppSettings";
#else
                var framework = IsILRepackIsolated ? "ConfigurationBridge" : "AppSettingsConfigResolveWhenUsed";
#endif

                Log.Error(ex, $"ConfigurationManagerStaticBridged: Failed to read '{key}' using {framework}. " +
                             $"Reading New Relic configuration values using {framework} will be disabled.");
                localConfigChecksDisabled = true;
                return null;
            }
        }
    }
}
