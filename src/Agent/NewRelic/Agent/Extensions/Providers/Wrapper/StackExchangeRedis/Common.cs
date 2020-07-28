using System;
using System.Reflection;
using NewRelic.Reflection;

namespace NewRelic.Providers.Wrapper.StackExchangeRedis
{
    public static class Common
    {
        private const string MessageTypeName = "StackExchange.Redis.Message";
        private const string CommandPropertyName = "Command";

        public const string RedisAssemblyName = "StackExchange.Redis";
        public const string RedisAssemblyStrongName = "StackExchange.Redis.StrongName";

        private static Func<object, Enum> _redisMessageCommandAccessor;
        private static Func<object, Enum> _strongNameMessageCommandAccessor;

        public static Func<object, Enum> GetMessageCommandAccessor(Assembly assembly)
        {
            var assemblyName = assembly.GetName().Name;
            switch (assemblyName)
            {
                case RedisAssemblyName:
                    return GetRedisMessageCommandAccessor();
                case RedisAssemblyStrongName:
                    return GetStrongNameMessageCommandAccessor();
            }

            throw new NotSupportedException($"The assembly provided does not have a command accessor implemented: {assemblyName}");
        }

        private static Func<object, Enum> GetRedisMessageCommandAccessor()
        {
            if (_redisMessageCommandAccessor == null)
            {
                _redisMessageCommandAccessor = VisibilityBypasser.Instance.GeneratePropertyAccessor<Enum>(RedisAssemblyName, Common.MessageTypeName, Common.CommandPropertyName);
            }

            return _redisMessageCommandAccessor;
        }

        private static Func<object, Enum> GetStrongNameMessageCommandAccessor()
        {
            if (_strongNameMessageCommandAccessor == null)
            {
                _strongNameMessageCommandAccessor = VisibilityBypasser.Instance.GeneratePropertyAccessor<Enum>(RedisAssemblyStrongName, Common.MessageTypeName, Common.CommandPropertyName);
            }

            return _strongNameMessageCommandAccessor;
        }
    }
}
