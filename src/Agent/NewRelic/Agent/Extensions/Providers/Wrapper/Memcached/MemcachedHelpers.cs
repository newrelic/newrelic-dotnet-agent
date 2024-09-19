// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Parsing;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Reflection;

namespace NewRelic.Providers.Wrapper.Memcached
{
    public class MemcachedHelpers
    {
        private static bool _hasGetServerFailed = false;
        private const string AssemblyName = "EnyimMemcachedCore";
        private static Func<object, object> _transformerGetter;
        private static Func<object, string, string> _transformMethod;
        private static Func<object, object> _poolGetter;
        private static Func<object, string, object> _locateMethod;
        private static Func<object, object> _endpointGetter;
        private static Func<object, object> _addressGetter;
        private static Func<object, int> _portGetter;

        public static ConnectionInfo GetConnectionInfo(string key, object target, IAgent agent)
        {
            if (_hasGetServerFailed)
            {
                return new ConnectionInfo(DatastoreVendor.Memcached.ToKnownName(), null, -1, null);
            }

            try
            {
                var targetType = target.GetType();
                _transformerGetter ??= VisibilityBypasser.Instance.GeneratePropertyAccessor<object>(targetType, "KeyTransformer");
                var transformer = _transformerGetter(target);

                _transformMethod ??= VisibilityBypasser.Instance.GenerateOneParameterMethodCaller<string, string>(AssemblyName, transformer.GetType().FullName, "Transform");
                var hashedKey = _transformMethod(transformer, key);

                _poolGetter ??= VisibilityBypasser.Instance.GeneratePropertyAccessor<object>(targetType, "Pool");
                var pool = _poolGetter(target);

                _locateMethod ??= VisibilityBypasser.Instance.GenerateOneParameterMethodCaller<string, object>(AssemblyName, pool.GetType().FullName, "Enyim.Caching.Memcached.IServerPool.Locate");
                var node = _locateMethod(pool, hashedKey);

                _endpointGetter ??= VisibilityBypasser.Instance.GeneratePropertyAccessor<object>(node.GetType(), "EndPoint");
                var endpoint = _endpointGetter(node);

                var endpointType = endpoint.GetType();
                _addressGetter ??= VisibilityBypasser.Instance.GeneratePropertyAccessor<object>(endpointType, "Address");
                var address = _addressGetter(endpoint).ToString();

                _portGetter ??= VisibilityBypasser.Instance.GeneratePropertyAccessor<int>(endpointType, "Port");
                int? port = _portGetter(endpoint);

                return new ConnectionInfo(DatastoreVendor.Memcached.ToKnownName(), address, port.HasValue ? port.Value : -1, null);
            }
            catch (Exception exception)
            {
                agent.Logger.Warn(exception, "Unable to get Memcached server address/port, likely to due to type differences. Server address/port will not be available.");
                _hasGetServerFailed = true;
                return new ConnectionInfo(DatastoreVendor.Memcached.ToKnownName(), null, -1, null);
            }
        }
    }
}
