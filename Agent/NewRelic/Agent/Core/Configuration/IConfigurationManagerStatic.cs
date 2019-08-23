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
		private readonly Func<String, String> _getAppSetting;

		public ConfigurationManagerStaticMock(Func<String, String> getAppSetting = null)
		{
			_getAppSetting = getAppSetting ?? (variable => null);
		}

		public String GetAppSetting(String variable)
		{
			return _getAppSetting(variable);
		}
	}

#if NET45

	public class ConfigurationManagerStatic : IConfigurationManagerStatic
	{
		public string GetAppSetting(string key)
		{
			if (key == null)
			{
				return null;
			}

			return System.Configuration.ConfigurationManager.AppSettings.Get(key);
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