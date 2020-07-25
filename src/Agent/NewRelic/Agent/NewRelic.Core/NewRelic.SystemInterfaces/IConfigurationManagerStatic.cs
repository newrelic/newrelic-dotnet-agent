using System;

#if NETSTANDARD2_0
using Microsoft.Extensions.Configuration;
using System.IO;
#endif

namespace NewRelic.SystemInterfaces
{
    public interface IConfigurationManagerStatic
    {
        string GetAppSetting(string key);
        int? GetAppSettingInt(string key);
    }

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

        public int? GetAppSettingInt(string key)
        {
            return null;
        }
    }

#if NET35

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

        public int? GetAppSettingInt(string key)
        {
            if (int.TryParse(GetAppSetting(key), out var value))
            {
                return value;
            }

            return null;
        }
    }
#else
    public class ConfigurationManagerStatic : IConfigurationManagerStatic
    {
        public string GetAppSetting(string key)
        {
            if (key == null)
            {
                return null;
            }

            // We're wrapping this in a try/catch to deal with the case where the necessary assemblies, in this case
            // Microsoft.Extensions.Configuration, aren't present in the application being instrumented
            try
            {
                return AppSettingsConfigResolveWhenUsed.GetAppSetting(key);
            }
            catch (Exception)
            {
                return null;
            }

        }

        public int? GetAppSettingInt(string key)
        {
            if (int.TryParse(GetAppSetting(key), out var value))
            {
                return value;
            }

            return null;
        }
    }
#endif
}
