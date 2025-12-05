// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NewRelic.Agent.Extensions.Logging;

#if NETSTANDARD2_0
using Microsoft.Extensions.Configuration;
using System.IO;
#endif

namespace NewRelic.Agent.Core.Configuration
{
    public interface IConfigurationManagerStatic
    {
        string AppSettingsFilePath { get; }
        string GetAppSetting(string key);
    }

    // sdaubin : Why do we have a mock in the agent code?  This is a code smell.
    public class ConfigurationManagerStaticMock : IConfigurationManagerStatic
    {
        private readonly Func<string, string> _getAppSetting;

        public ConfigurationManagerStaticMock(Func<string, string> getAppSetting = null)
        {
            _getAppSetting = getAppSetting ?? (key => null);
        }

        public string AppSettingsFilePath => throw new NotImplementedException();

        public string GetAppSetting(string key)
        {
            return _getAppSetting(key);
        }
    }

#if NETFRAMEWORK

    public class ConfigurationManagerStatic : IConfigurationManagerStatic
    {
        private bool localConfigChecksDisabled;

        public string AppSettingsFilePath
        {
            get
            {
                try
                {
                    if (!localConfigChecksDisabled)
                        return AppDomain.CurrentDomain.SetupInformation.ConfigurationFile;
                }
                catch (Exception ex)
                {
                    // Ignore exceptions when accessing configuration file path
                    Log.Debug(ex, "Failed to get application configuration file path");
                }

                return null;
            }
        }

        public string GetAppSetting(string key)
        {
            if (localConfigChecksDisabled || key == null) return null;

            try
            {
                return System.Configuration.ConfigurationManager.AppSettings.Get(key);
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Failed to read '{key}' using System.Configuration.ConfigurationManager.AppSettings. Reading New Relic configuration values using System.Configuration.ConfigurationManager.AppSettings will be disabled.");
                localConfigChecksDisabled = true;
                return null;
            }
        }
    }
#else
    /// <summary>
    /// Provides configuration access for .NET Standard applications.
    /// Uses internal bridging logic to access the application's Microsoft.Extensions.Configuration
    /// system when available, with fallback to ILRepacked configuration.
    /// </summary>
    public class ConfigurationManagerStatic : IConfigurationManagerStatic
    {
        private bool localConfigChecksDisabled;

        public string AppSettingsFilePath
        {
            get
            {
                try
                {
                    if (!localConfigChecksDisabled)
                    {
                        // Use internal bridge to access application's configuration
                        try
                        {
                            return ConfigurationBridge.GetAppSettingsFilePath();
                        }
                        catch
                        {
                            // Fall back to existing logic
                            return AppSettingsConfigResolveWhenUsed.AppSettingsFilePath;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Debug(ex, "ConfigurationManagerStatic: Error getting AppSettingsFilePath.");
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
                // Use internal bridge first to access application's configuration
                try
                {
                    return ConfigurationBridge.GetAppSetting(key);
                }
                catch
                {
                    // Fall back to existing logic - We're wrapping this in a try/catch to deal with the case where the necessary assemblies, in this case
                    // Microsoft.Extensions.Configuration, aren't present in the application being instrumented
                    try
                    {
                        return AppSettingsConfigResolveWhenUsed.GetAppSetting(key);
                    }
                    catch (FileNotFoundException e)
                    {
                        Log.Debug(e, "appsettings.json will not be searched for config values because this application does not reference: {e.FileName}.");
                        localConfigChecksDisabled = true;
                        return null;
                    }
                    catch (Exception e)
                    {
                        Log.Debug(e, "appsettings.json will not be searched for config values because an error was encountered");
                        localConfigChecksDisabled = true;
                        return null;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"ConfigurationManagerStatic: Failed to read '{key}'. " +
                             $"Reading New Relic configuration values will be disabled.");
                localConfigChecksDisabled = true;
                return null;
            }
        }
    }
#endif
}
