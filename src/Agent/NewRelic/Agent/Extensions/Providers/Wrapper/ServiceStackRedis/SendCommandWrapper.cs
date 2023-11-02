// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.SystemExtensions;
using NewRelic.Parsing.ConnectionString;
using NewRelic.Reflection;
using NewRelic.Agent.Extensions.Parsing;
using NewRelic.Agent.Api;

namespace NewRelic.Providers.Wrapper.ServiceStackRedis
{
    public class SendCommandWrapper : IWrapper
    {
        private const string AssemblyName = "ServiceStack.Redis";
        private const string TypeName = "ServiceStack.Redis.RedisClient";
        private const string PropertyHost = "Host";
        private const string PropertyPortPathOrId = "Port";
        private const string PropertyDatabaseName = "Db";

        public bool IsTransactionRequired => true;

        private static class Statics
        {
            private static Func<object, string> _propertyHost;
            private static Func<object, int> _propertyPortPathOrId;
            private static Func<object, long> _propertyDatabaseName;

            public static readonly Func<object, string> GetPropertyHost = AssignPropertyHost();

            public static readonly Func<object, int> GetPropertyPortPathOrId = AssignPropertyPortPathOrId();

            public static readonly Func<object, long> GetPropertyDatabaseName = AssignPropertyDatabaseName();

            private static Func<object, string> AssignPropertyHost()
            {
                return _propertyHost ?? (_propertyHost = VisibilityBypasser.Instance.GeneratePropertyAccessor<string>(AssemblyName, TypeName, PropertyHost));
            }

            private static Func<object, int> AssignPropertyPortPathOrId()
            {
                return _propertyPortPathOrId ?? (_propertyPortPathOrId = VisibilityBypasser.Instance.GeneratePropertyAccessor<int>(AssemblyName, TypeName, PropertyPortPathOrId));
            }

            private static Func<object, long> AssignPropertyDatabaseName()
            {
                return _propertyDatabaseName ?? (_propertyDatabaseName = VisibilityBypasser.Instance.GeneratePropertyAccessor<long>(AssemblyName, TypeName, PropertyDatabaseName));
            }
        }

        public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
        {
            var method = methodInfo.Method;
            var canWrap = method.MatchesAny(assemblyName: "ServiceStack.Redis", typeName: "ServiceStack.Redis.RedisNativeClient", methodName: "SendCommand");
            return new CanWrapResponse(canWrap);
        }

        static string GetRedisCommand(byte[] command)
        {
            // ServiceStack.Redis uses the same UTF8 encoder
            return System.Text.Encoding.UTF8.GetString(command);
        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
        {
            var redisCommandWithArgumentsAsBytes = instrumentedMethodCall.MethodCall.MethodArguments.ExtractNotNullAs<byte[][]>(0);
            var redisCommand = redisCommandWithArgumentsAsBytes[0];
            if (redisCommand == null)
                return Delegates.NoOp;

            var operation = GetRedisCommand(redisCommand);
            var contextObject = instrumentedMethodCall.MethodCall.InvocationTarget;
            if (contextObject == null)
                throw new NullReferenceException(nameof(contextObject));

            var host = TryGetPropertyName(PropertyHost, contextObject) ?? "unknown";
            host = ConnectionStringParserHelper.NormalizeHostname(host, agent.Configuration.UtilizationHostName);
            var port = TryGetPropertyName(PropertyPortPathOrId, contextObject);
            if (!int.TryParse(port, out int portNum))
            {
                portNum = -1;
            }
            var databaseName = TryGetPropertyName(PropertyDatabaseName, contextObject);
            var connectionInfo = new ConnectionInfo(DatastoreVendor.Redis.ToKnownName(), host, portNum, databaseName);

            var segment = transaction.StartDatastoreSegment(instrumentedMethodCall.MethodCall, ParsedSqlStatement.FromOperation(DatastoreVendor.Redis, operation), connectionInfo);

            return Delegates.GetDelegateFor(segment);
        }

        private static string TryGetPropertyName(string propertyName, object contextObject)
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
