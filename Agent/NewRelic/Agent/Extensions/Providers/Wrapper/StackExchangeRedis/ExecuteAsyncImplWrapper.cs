using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Parsing;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Parsing.ConnectionString;
using NewRelic.Reflection;
using System;

namespace NewRelic.Providers.Wrapper.StackExchangeRedis
{
	public class ExecuteAsyncImplWrapper : IWrapper
	{
		private const string TypeName = "StackExchange.Redis.ConnectionMultiplexer";
		private const string PropertyConfiguration = "Configuration";
		private static string _assemblyName;

		private static class Statics
		{
			private static Func<object, string> _propertyConfiguration;

			public static readonly Func<object, string> GetPropertyConfiguration = AssignPropertyConfiguration();

			private static Func<object, string> AssignPropertyConfiguration()
			{
				return _propertyConfiguration ??
					(_propertyConfiguration =
					VisibilityBypasser.Instance.GeneratePropertyAccessor<string>(_assemblyName, TypeName, PropertyConfiguration));
			}
		}

		public bool IsTransactionRequired => true;

		public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
		{
			var method = methodInfo.Method;
			var canWrap = method.MatchesAny(
				assemblyNames: Common.AssemblyNames,
				typeNames: new[] { "StackExchange.Redis.ConnectionMultiplexer" },
				methodNames: new[] { "ExecuteAsyncImpl" }
			);

			return new CanWrapResponse(canWrap);
		}

		public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
		{
			if (instrumentedMethodCall.IsAsync)
			{
				transaction.AttachToAsync();
			}

			var operation = Common.GetRedisCommand(instrumentedMethodCall.MethodCall);

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
			return Delegates.GetAsyncDelegateFor(agent, segment);
		}

		private static string TryGetPropertyName(string propertyName, object contextObject)
		{
			if (propertyName == PropertyConfiguration)
			{
				return Statics.GetPropertyConfiguration(contextObject);
			}

			throw new Exception("Unexpected instrumented property in wrapper: " + contextObject + "." + propertyName);
		}

		private static string AssignFullName(InstrumentedMethodCall instrumentedMethodCall)
		{
			return _assemblyName ?? (_assemblyName = Common.ParseFullName(instrumentedMethodCall.MethodCall.Method.Type.Assembly.FullName));
		}
	}
}
