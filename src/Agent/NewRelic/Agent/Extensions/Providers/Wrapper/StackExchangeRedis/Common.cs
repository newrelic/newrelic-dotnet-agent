// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Extensions.Parsing;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Parsing.ConnectionString;
using NewRelic.Reflection;
using System;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Net;

namespace NewRelic.Providers.Wrapper.StackExchangeRedis
{
    public static class Common
    {
        private const string MessageTypeName = "StackExchange.Redis.Message";
        private const string CommandPropertyName = "Command";

        public const string ConnectionMultiplexerTypeName = "StackExchange.Redis.ConnectionMultiplexer";
        private const string RawConfigPropertyName = "RawConfig";

        private const string ConfigurationOptionsTypeName = "StackExchange.Redis.ConfigurationOptions";
        private const string EndPointsPropertyName = "EndPoints";

        public const string RedisAssemblyName = "StackExchange.Redis";
        public const string RedisAssemblyStrongName = "StackExchange.Redis.StrongName";

        private static Func<object, Enum> _redisMessageCommandAccessor;
        private static Func<object, Enum> _strongNameMessageCommandAccessor;

        private static Func<object, object> _redisRawConfigAccessor;
        private static Func<object, object> _strongNameRawConfigAccessor;

        private static Func<object, Collection<EndPoint>> _redisEndPointsAccessor;
        private static Func<object, Collection<EndPoint>> _strongNameEndPointsAccessor;

        //We're not using the EnumNameCache here because we do not have access to the compile time type of the enum, and need
        //to use the parent Enum type instead. If we used the EnumNameCache with the Enum type as the type parameter, we could
        //run into conflicts with other instrumentation that can only access the parent Enum type.
        private static readonly ConcurrentDictionary<Enum, string> _commandNameCache = new ConcurrentDictionary<Enum, string>();

        private static Func<object, Enum> GetMessageCommandAccessor(string assemblyName)
        {
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

        public static string GetRedisCommand(MethodCall methodCall, string assemblyName)
        {
            // instrumentedMethodCall.MethodCall.MethodArguments[0] returns an object representing a StackExchange.Redis.Message object
            var message = methodCall.MethodArguments[0];
            if (message == null)
                throw new NullReferenceException("message");

            var getCommand = GetMessageCommandAccessor(assemblyName);

            var command = getCommand(message);
            return _commandNameCache.GetOrAdd(command, GetCommandNameFromEnumValue);
        }

        public static ConnectionInfo GetConnectionInfoFromConnectionMultiplexer(MethodCall methodCall, string assemblyName, string utilizationHostName)
        {
            var connectionMultiplexer = methodCall.InvocationTarget;
            var rawConfig = GetRawConfigAccessor(assemblyName)(connectionMultiplexer);
            var endpoints = GetEndPointsAccessor(assemblyName)(rawConfig);

            if (endpoints == null || endpoints.Count <= 0)
            {
                return null;
            }

            var endpoint = endpoints[0];

            var dnsEndpoint = endpoint as DnsEndPoint;
            var ipEndpoint = endpoint as IPEndPoint;

            int port = -1;
            string host = null;

            if (dnsEndpoint != null)
            {
                port = dnsEndpoint.Port;
                host = ConnectionStringParserHelper.NormalizeHostname(dnsEndpoint.Host, utilizationHostName);
            }

            if (ipEndpoint != null)
            {
                port = ipEndpoint.Port;
                host = ConnectionStringParserHelper.NormalizeHostname(ipEndpoint.Address.ToString(), utilizationHostName);
            }

            if (host == null)
            {
                return null;
            }

            return new ConnectionInfo(DatastoreVendor.Redis.ToKnownName(), host, port, null, null);
        }

        private static string GetCommandNameFromEnumValue(Enum commandValue)
        {
            return commandValue.ToString();
        }

        private static Func<object, object> GetRawConfigAccessor(string assemblyName)
        {
            switch (assemblyName)
            {
                case RedisAssemblyName:
                    return GetRawConfigAccessor();
                case RedisAssemblyStrongName:
                    return GetStrongNameRawConfigAccessor();
            }

            throw new NotSupportedException($"The assembly provided does not have a RawConfig accessor implemented: {assemblyName}");
        }

        private static Func<object, object> GetRawConfigAccessor()
        {
            if (_redisRawConfigAccessor == null)
            {
                _redisRawConfigAccessor = VisibilityBypasser.Instance.GeneratePropertyAccessor<object>(RedisAssemblyName, Common.ConnectionMultiplexerTypeName, Common.RawConfigPropertyName);
            }

            return _redisRawConfigAccessor;
        }

        private static Func<object, object> GetStrongNameRawConfigAccessor()
        {
            if (_strongNameRawConfigAccessor == null)
            {
                _strongNameRawConfigAccessor = VisibilityBypasser.Instance.GeneratePropertyAccessor<object>(RedisAssemblyStrongName, Common.ConnectionMultiplexerTypeName, Common.RawConfigPropertyName);
            }

            return _strongNameRawConfigAccessor;
        }

        private static Func<object, Collection<EndPoint>> GetEndPointsAccessor(string assemblyName)
        {
            switch (assemblyName)
            {
                case RedisAssemblyName:
                    return GetEndPointsAccessor();
                case RedisAssemblyStrongName:
                    return GetStrongNameEndPointsAccessor();
            }

            throw new NotSupportedException($"The assembly provided does not have a EndPoints accessor implemented: {assemblyName}");
        }

        private static Func<object, Collection<EndPoint>> GetEndPointsAccessor()
        {
            if (_redisEndPointsAccessor == null)
            {
                _redisEndPointsAccessor = VisibilityBypasser.Instance.GeneratePropertyAccessor<Collection<EndPoint>>(RedisAssemblyName, Common.ConfigurationOptionsTypeName, Common.EndPointsPropertyName);
            }

            return _redisEndPointsAccessor;
        }

        private static Func<object, Collection<EndPoint>> GetStrongNameEndPointsAccessor()
        {
            if (_strongNameEndPointsAccessor == null)
            {
                _strongNameEndPointsAccessor = VisibilityBypasser.Instance.GeneratePropertyAccessor<Collection<EndPoint>>(RedisAssemblyStrongName, Common.ConfigurationOptionsTypeName, Common.EndPointsPropertyName);
            }

            return _strongNameEndPointsAccessor;
        }
    }
}
