/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
using System;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Parsing.ConnectionString;
using NewRelic.Reflection;

namespace NewRelic.Providers.Wrapper.StackExchangeRedis
{
    public class ExecuteSyncImplWrapper : IWrapper
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
        private static string GetRedisCommand(MethodCall methodCall)
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
            var connectionInfo = (ConnectionInfo)transaction.GetOrSetValueFromCache(connectionOptions, GetConnectionInfo);

            var segment = transaction.StartDatastoreSegment(instrumentedMethodCall.MethodCall, operation, DatastoreVendor.Redis, host: connectionInfo.Host, portPathOrId: connectionInfo.PortPathOrId, databaseName: connectionInfo.DatabaseName);
            return Delegates.GetDelegateFor(segment);
        }
        private static string TryGetPropertyName(string propertyName, object contextObject)
        {
            if (propertyName == PropertyConfiguration)
                return Statics.GetPropertyConfiguration(contextObject);

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
