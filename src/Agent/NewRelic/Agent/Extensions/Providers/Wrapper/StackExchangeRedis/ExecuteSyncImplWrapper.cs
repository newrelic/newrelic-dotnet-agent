using System;
using System.Diagnostics;
using JetBrains.Annotations;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Parsing.ConnectionString;
using NewRelic.Reflection;

namespace NewRelic.Providers.Wrapper.StackExchangeRedis
{
	public class ExecuteSyncImplWrapper : IWrapper
	{
		private const String TypeName = "StackExchange.Redis.ConnectionMultiplexer";
		private const String PropertyConfiguration = "Configuration";
		private static String _assemblyName;

		private static class Statics
		{
			private static Func<Object, String> _propertyConfiguration;

			[NotNull]
			public static readonly Func<Object, String> GetPropertyConfiguration = AssignPropertyConfiguration();

			private static Func<Object, String> AssignPropertyConfiguration()
			{
				return _propertyConfiguration ??
					(_propertyConfiguration =
					VisibilityBypasser.Instance.GeneratePropertyAccessor<String>(_assemblyName, TypeName, PropertyConfiguration));
			}
		}

		private static readonly String[] AssemblyNames = {
			Common.RedisAssemblyName,
			Common.RedisAssemblyStrongName
		};

		public bool IsTransactionRequired => true;

		public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
		{
			var method = methodInfo.Method;
			var canWrap = method.MatchesAny(
				assemblyNames: AssemblyNames, 
				typeNames: new[] { "StackExchange.Redis.ConnectionMultiplexer" }, 
				methodNames: new[] { "ExecuteSyncImpl" }
			);
			
			return new CanWrapResponse(canWrap);
		}

		[NotNull]
		private static String GetRedisCommand([NotNull] MethodCall methodCall)
		{
			// instrumentedMethodCall.MethodCall.MethodArguments[0] returns an Object representing a StackExchange.Redis.Message object
			var message = methodCall.MethodArguments[0];
			if (message == null)
				throw new NullReferenceException("message");
			
			var getCommand = Common.GetMessageCommandAccessor(methodCall.Method.Type.Assembly);

			var command = getCommand(message);
			return command.ToString();
		}

		public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgentWrapperApi agentWrapperApi, ITransaction transaction)
		{
			var operation = GetRedisCommand(instrumentedMethodCall.MethodCall);

			//calling here to setup a static prior to actual bypasser init to speed up all subsequent calls..
			AssignFullName(instrumentedMethodCall);
			var connectionOptions = TryGetPropertyName(PropertyConfiguration, instrumentedMethodCall.MethodCall.InvocationTarget);
			object GetConnectionInfo() => ConnectionInfo.FromConnectionString(DatastoreVendor.Redis, connectionOptions);
			var connectionInfo = (ConnectionInfo) transaction.GetOrSetValueFromCache(connectionOptions, GetConnectionInfo);

			var segment = transaction.StartDatastoreSegment(instrumentedMethodCall.MethodCall, operation, DatastoreVendor.Redis, host: connectionInfo.Host, portPathOrId:connectionInfo.PortPathOrId, databaseName:connectionInfo.DatabaseName);
			return Delegates.GetDelegateFor(segment);
		}

		[CanBeNull]
		private static String TryGetPropertyName([NotNull] String propertyName, [NotNull] Object contextObject)
		{
			if (propertyName == PropertyConfiguration)
				return Statics.GetPropertyConfiguration(contextObject);

				throw new Exception("Unexpected instrumented property in wrapper: " + contextObject + "." + propertyName);
		}

		private static String AssignFullName(InstrumentedMethodCall instrumentedMethodCall)
		{
			return _assemblyName ?? (_assemblyName = ParseFullName(instrumentedMethodCall.MethodCall.Method.Type.Assembly.FullName));
		}

		private static String ParseFullName(String fullName)
		{
			return fullName.Contains(Common.RedisAssemblyStrongName) ? Common.RedisAssemblyStrongName : Common.RedisAssemblyName;
		}
	}
}
