using System;
using JetBrains.Annotations;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Parsing.ConnectionString;
using NewRelic.Reflection;
using NewRelic.Agent.Extensions.Parsing;

namespace NewRelic.Providers.Wrapper.StackExchangeRedis
{
	public class ExecuteSyncImplWrapper : IWrapper
	{
		private const string TypeName = "StackExchange.Redis.ConnectionMultiplexer";
		private const string PropertyConfiguration = "Configuration";
		private static string _assemblyName;

		private static class Statics
		{
			private static Func<Object, string> _propertyConfiguration;

			[NotNull]
			public static readonly Func<Object, string> GetPropertyConfiguration = AssignPropertyConfiguration();

			private static Func<Object, string> AssignPropertyConfiguration()
			{
				return _propertyConfiguration ??
					(_propertyConfiguration =
					VisibilityBypasser.Instance.GeneratePropertyAccessor<string>(_assemblyName, TypeName, PropertyConfiguration));
			}
		}

		private static readonly string[] AssemblyNames = {
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
		private static string GetRedisCommand([NotNull] MethodCall methodCall)
		{
			// instrumentedMethodCall.MethodCall.MethodArguments[0] returns an Object representing a StackExchange.Redis.Message object
			var message = methodCall.MethodArguments[0];
			if (message == null)
				throw new NullReferenceException("message");

			var getCommand = Common.GetMessageCommandAccessor(methodCall.Method.Type.Assembly);

			var command = getCommand(message);
			return command.ToString();
		}

		public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
		{
			var operation = GetRedisCommand(instrumentedMethodCall.MethodCall);

			//calling here to setup a static prior to actual bypasser init to speed up all subsequent calls..
			AssignFullName(instrumentedMethodCall);
			var connectionOptions = TryGetPropertyName(PropertyConfiguration, instrumentedMethodCall.MethodCall.InvocationTarget);
			object GetConnectionInfo() => ConnectionInfoParser.FromConnectionString(DatastoreVendor.Redis, connectionOptions);

			ConnectionInfo connectionInfo = null;
			if (connectionOptions != null)
			{
				connectionInfo = (ConnectionInfo)transaction.GetOrSetValueFromCache(connectionOptions, GetConnectionInfo);
			}

			var segment = transaction.StartDatastoreSegment(instrumentedMethodCall.MethodCall, ParsedSqlStatement.FromOperation(DatastoreVendor.Redis, operation), connectionInfo);
			return Delegates.GetDelegateFor(segment);
		}

		[CanBeNull]
		private static string TryGetPropertyName([NotNull] string propertyName, [NotNull] Object contextObject)
		{
			if (propertyName == PropertyConfiguration)
			{
				return Statics.GetPropertyConfiguration(contextObject);
			}

			throw new Exception("Unexpected instrumented property in wrapper: " + contextObject + "." + propertyName);
		}

		private static string AssignFullName(InstrumentedMethodCall instrumentedMethodCall)
		{
			return _assemblyName ?? (_assemblyName = ParseFullName(instrumentedMethodCall.MethodCall.Method.Type.Assembly.FullName));
		}

		private static string ParseFullName(string fullName)
		{
			return fullName.Contains(Common.RedisAssemblyStrongName) ? Common.RedisAssemblyStrongName : Common.RedisAssemblyName;
		}
	}
}