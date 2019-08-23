using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Reflection;
using System;
using System.Reflection;

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

		public static readonly string[] AssemblyNames = {
			RedisAssemblyName,
			RedisAssemblyStrongName
		};

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

		public static string GetRedisCommand(MethodCall methodCall)
		{
			// instrumentedMethodCall.MethodCall.MethodArguments[0] returns an object representing a StackExchange.Redis.Message object
			var message = methodCall.MethodArguments[0];
			if (message == null)
				throw new NullReferenceException("message");

			var getCommand = GetMessageCommandAccessor(methodCall.Method.Type.Assembly);

			var command = getCommand(message);
			return command.ToString();
		}

		public static string ParseFullName(string fullName)
		{
			return fullName.Contains(RedisAssemblyStrongName) ? RedisAssemblyStrongName : RedisAssemblyName;
		}
	}
}
