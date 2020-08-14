// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NewRelic.Core.Logging;

#if NETSTANDARD2_0
using Microsoft.Extensions.Configuration;
using System.IO;
#endif

namespace NewRelic.Agent.Core.Configuration
{
    public interface IConfigurationManagerStatic
    {
        string GetAppSetting(string key);
    }

    // sdaubin : Why do we have a mock in the agent code?  This is a code smell.
    public class ConfigurationManagerStaticMock : IConfigurationManagerStatic
    {
        private readonly Func<string, string> _getAppSetting;

        public ConfigurationManagerStaticMock(Func<string, string> getAppSetting = null)
        {
            _getAppSetting = getAppSetting ?? (variable => null);
        }

        public string GetAppSetting(string variable)
        {
            return _getAppSetting(variable);
        }
    }

#if NET45

	public class ConfigurationManagerStatic : IConfigurationManagerStatic
	{
		private bool localConfigChecksDisabled;

		public string GetAppSetting(string key)
		{
			if (localConfigChecksDisabled || key == null) return null;

			try
			{
				return System.Configuration.ConfigurationManager.AppSettings.Get(key);
			}
			catch (Exception ex)
			{
				Log.Error($"Failed to read '{key}' using System.Configuration.ConfigurationManager.AppSettings. Reading New Relic configuration values using System.Configuration.ConfigurationManager.AppSettings will be disabled. Exception: {ex}");
				localConfigChecksDisabled = true;
				return null;
			}
		}
	}
#else
    public class ConfigurationManagerStatic : IConfigurationManagerStatic
    {
        private bool localConfigChecksDisabled;

        public string GetAppSetting(string key)
        {
            if (localConfigChecksDisabled || key == null) return null;

            // We're wrapping this in a try/catch to deal with the case where the necessary assemblies, in this case
            // Microsoft.Extensions.Configuration, aren't present in the application being instrumented
            try
            {
                return AppSettingsConfigResolveWhenUsed.GetAppSetting(key);
            }
            catch (FileNotFoundException e)
            {
                if (Log.IsDebugEnabled) Log.Debug($"appsettings.json will not be searched for config values because this application does not reference: {e.FileName}.");
                localConfigChecksDisabled = true;
                return null;
            }
            catch (Exception e)
            {
                if (Log.IsDebugEnabled) Log.Debug($"appsettings.json will not be searched for config values because an error was encountered: {e}");
                localConfigChecksDisabled = true;
                return null;
            }
        }
    }
#endif
}
