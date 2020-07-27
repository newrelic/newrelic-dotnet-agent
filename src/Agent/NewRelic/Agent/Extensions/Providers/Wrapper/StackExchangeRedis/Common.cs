using System;
using System.Reflection;
using JetBrains.Annotations;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Reflection;

namespace NewRelic.Providers.Wrapper.StackExchangeRedis
{
    public static class Common
    {
        private const String MessageTypeName = "StackExchange.Redis.Message";
        private const String CommandPropertyName = "Command";

        public const String RedisAssemblyName = "StackExchange.Redis";
        public const String RedisAssemblyStrongName = "StackExchange.Redis.StrongName";

        private static Func<Object, Enum> _redisMessageCommandAccessor;
        private static Func<Object, Enum> _strongNameMessageCommandAccessor;

        public static Func<Object, Enum> GetMessageCommandAccessor(Assembly assembly)
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

        private static Func<Object, Enum> GetRedisMessageCommandAccessor()
        {
            if (_redisMessageCommandAccessor == null)
            {
                _redisMessageCommandAccessor = VisibilityBypasser.Instance.GeneratePropertyAccessor<Enum>(RedisAssemblyName, Common.MessageTypeName, Common.CommandPropertyName);
            }

            return _redisMessageCommandAccessor;
        }

        private static Func<Object, Enum> GetStrongNameMessageCommandAccessor()
        {
            if (_strongNameMessageCommandAccessor == null)
            {
                _strongNameMessageCommandAccessor = VisibilityBypasser.Instance.GeneratePropertyAccessor<Enum>(RedisAssemblyStrongName, Common.MessageTypeName, Common.CommandPropertyName);
            }

            return _strongNameMessageCommandAccessor;
        }
    }
}
