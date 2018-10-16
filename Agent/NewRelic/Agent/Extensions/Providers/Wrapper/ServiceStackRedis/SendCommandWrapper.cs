using System;
using System.Diagnostics;
using JetBrains.Annotations;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.SystemExtensions;
using NewRelic.Parsing.ConnectionString;
using NewRelic.Reflection;
using NewRelic.Agent.Extensions.Parsing;

namespace NewRelic.Providers.Wrapper.ServiceStackRedis
{
	public class SendCommandWrapper : IWrapper
	{
		private const String AssemblyName = "ServiceStack.Redis";
		private const String TypeName = "ServiceStack.Redis.RedisClient";
		private const String PropertyHost = "Host";
		private const String PropertyPortPathOrId = "Port";
		private const String PropertyDatabaseName = "Db";

		public bool IsTransactionRequired => true;

		private static class Statics
		{
			private static Func<Object, String> _propertyHost;
			private static Func<Object, Int32> _propertyPortPathOrId;
			private static Func<Object, Int64> _propertyDatabaseName;

			[NotNull]
			public static readonly Func<Object, String> GetPropertyHost = AssignPropertyHost();
			[NotNull]
			public static readonly Func<Object, Int32> GetPropertyPortPathOrId = AssignPropertyPortPathOrId();
			[NotNull]
			public static readonly Func<Object, Int64> GetPropertyDatabaseName = AssignPropertyDatabaseName();

			private static Func<Object, String> AssignPropertyHost()
			{
				return _propertyHost ?? (_propertyHost = VisibilityBypasser.Instance.GeneratePropertyAccessor<String>(AssemblyName, TypeName, PropertyHost));
			}

			private static Func<Object, Int32> AssignPropertyPortPathOrId()
			{
				return _propertyPortPathOrId ?? (_propertyPortPathOrId = VisibilityBypasser.Instance.GeneratePropertyAccessor<Int32>(AssemblyName, TypeName, PropertyPortPathOrId));
			}

			private static Func<Object, Int64> AssignPropertyDatabaseName()
			{
				return _propertyDatabaseName ?? (_propertyDatabaseName = VisibilityBypasser.Instance.GeneratePropertyAccessor<Int64>(AssemblyName, TypeName, PropertyDatabaseName));
			}
		}

		public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
		{
			var method = methodInfo.Method;
			var canWrap = method.MatchesAny(assemblyName: "ServiceStack.Redis", typeName: "ServiceStack.Redis.RedisNativeClient", methodName: "SendCommand");
			return new CanWrapResponse(canWrap);
		}

		[NotNull]
		static String GetRedisCommand([NotNull] Byte[] command)
		{
			// ServiceStack.Redis uses the same UTF8 encoder
			return System.Text.Encoding.UTF8.GetString(command);
		}

		public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgentWrapperApi agentWrapperApi, ITransactionWrapperApi transactionWrapperApi)
		{
			var redisCommandWithArgumentsAsBytes = instrumentedMethodCall.MethodCall.MethodArguments.ExtractNotNullAs<Byte[][]>(0);
			var redisCommand = redisCommandWithArgumentsAsBytes[0];
			if (redisCommand == null)
				return Delegates.NoOp;

			var operation = GetRedisCommand(redisCommand);
			var contextObject = instrumentedMethodCall.MethodCall.InvocationTarget;
			if (contextObject == null)
				throw new NullReferenceException(nameof(contextObject));

			var host = TryGetPropertyName(PropertyHost, contextObject) ?? "unknown";
			host = ConnectionStringParserHelper.NormalizeHostname(host);
			var portPathOrId = TryGetPropertyName(PropertyPortPathOrId, contextObject);
			var databaseName = TryGetPropertyName(PropertyDatabaseName, contextObject);
			var connectionInfo = new ConnectionInfo(host, portPathOrId, databaseName);

			var segment = transactionWrapperApi.StartDatastoreSegment(instrumentedMethodCall.MethodCall, ParsedSqlStatement.FromOperation(DatastoreVendor.Redis, operation), connectionInfo);

			return Delegates.GetDelegateFor(segment);
		}

		[CanBeNull]
		private static String TryGetPropertyName([NotNull] String propertyName, [NotNull] Object contextObject)
		{
			if (propertyName == PropertyHost)
				return Statics.GetPropertyHost(contextObject);
			if (propertyName == PropertyPortPathOrId)
				return Statics.GetPropertyPortPathOrId(contextObject).ToString();
			if (propertyName == PropertyDatabaseName)
				return Statics.GetPropertyDatabaseName(contextObject).ToString();

			throw new Exception("Unexpected instrumented property in wrapper: " + contextObject + "." + propertyName);
		}
	}
}
