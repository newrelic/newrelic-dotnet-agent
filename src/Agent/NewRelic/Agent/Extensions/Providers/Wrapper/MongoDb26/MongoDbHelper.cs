// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Net;
using NewRelic.Agent.Extensions.Parsing;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Parsing.ConnectionString;
using NewRelic.Reflection;

namespace NewRelic.Providers.Wrapper.MongoDb26
{
    public static class MongoDbHelper
    {
        private static readonly ConcurrentDictionary<Type, Func<object, object>> _collectionNamespaceGetterMap = new ConcurrentDictionary<Type, Func<object, object>>();
        private static readonly ConcurrentDictionary<Type, Func<object, object>> _collectionGetterMap = new ConcurrentDictionary<Type, Func<object, object>>();
        private static readonly ConcurrentDictionary<Type, Func<object, object>> _channelSourceGetterMap = new ConcurrentDictionary<Type, Func<object, object>>();
        private static readonly ConcurrentDictionary<Type, Func<object, object>> _databaseGetterMap = new ConcurrentDictionary<Type, Func<object, object>>();
        private static readonly ConcurrentDictionary<Type, Func<object, object>> _serverGetterMap = new ConcurrentDictionary<Type, Func<object, object>>();

        private static Func<object, string> _getCollectionName;
        private static Func<object, object> _getDatabaseNamespaceFromCollectionNamespace;
        private static Func<object, object> _getDatabaseNamespaceFromDatabase;
        private static Func<object, string> _getDatabaseName;
        private static Func<object, EndPoint> _getEndPoint;
        private static Func<object, object> _getClient;
        private static Func<object, object> _getSettings;
        private static Func<object, IList> _getServers;
        private static Func<object, string> _getHost;
        private static Func<object, int> _getPort;


        public static string GetCollectionName(object collectionNamespace)
        {
            var getter = _getCollectionName ?? (_getCollectionName = VisibilityBypasser.Instance.GeneratePropertyAccessor<string>("MongoDB.Driver.Core", "MongoDB.Driver.CollectionNamespace", "CollectionName"));
            return getter(collectionNamespace);
        }

        public static string GetDatabaseNameFromCollectionNamespace(object collectionNamespace)
        {
            var databaseNamespaceGetter = _getDatabaseNamespaceFromCollectionNamespace ?? (_getDatabaseNamespaceFromCollectionNamespace = VisibilityBypasser.Instance.GeneratePropertyAccessor<object>("MongoDB.Driver.Core", "MongoDB.Driver.CollectionNamespace", "DatabaseNamespace"));
            var databaseNamespace = databaseNamespaceGetter(collectionNamespace);

            return GetDatabaseNameFromDatabaseNamespace(databaseNamespace);
        }

        public static string GetDatabaseNameFromDatabase(object database)
        {
            var databaseNamespaceGetter = _getDatabaseNamespaceFromDatabase ?? (_getDatabaseNamespaceFromDatabase = VisibilityBypasser.Instance.GeneratePropertyAccessor<object>("MongoDB.Driver", "MongoDB.Driver.MongoDatabaseImpl", "DatabaseNamespace"));
            var databaseNamespace = databaseNamespaceGetter(database);

            return GetDatabaseNameFromDatabaseNamespace(databaseNamespace);
        }

        private static string GetDatabaseNameFromDatabaseNamespace(object databaseNamespace)
        {
            var databaseNameGetter = _getDatabaseName ?? (_getDatabaseName = VisibilityBypasser.Instance.GeneratePropertyAccessor<string>("MongoDB.Driver.Core", "MongoDB.Driver.DatabaseNamespace", "DatabaseName"));
            return databaseNameGetter(databaseNamespace);
        }

        private static EndPoint GetEndPoint(object owner)
        {
            var getter = _getEndPoint ?? (_getEndPoint = VisibilityBypasser.Instance.GeneratePropertyAccessor<EndPoint>("MongoDB.Driver.Core", "MongoDB.Driver.Core.Servers.Server", "EndPoint"));
            return getter(owner);
        }

        public static object GetCollectionNamespaceFieldFromGeneric(object owner)
        {
            var getter = _collectionNamespaceGetterMap.GetOrAdd(owner.GetType(), t => VisibilityBypasser.Instance.GenerateFieldReadAccessor<object>(t, "_collectionNamespace"));
            return getter(owner);
        }

        public static object GetCollectionNamespacePropertyFromGeneric(object owner)
        {
            var getter = _collectionNamespaceGetterMap.GetOrAdd(owner.GetType(), t => VisibilityBypasser.Instance.GeneratePropertyAccessor<object>(t, "CollectionNamespace"));
            return getter(owner);
        }

        public static object GetCollectionFieldFromGeneric(object owner)
        {
            var getter = _collectionGetterMap.GetOrAdd(owner.GetType(), t => VisibilityBypasser.Instance.GenerateFieldReadAccessor<object>(t, "_collection"));
            return getter(owner);
        }

        private static object GetChannelSourceFieldFromGeneric(object owner)
        {
            var getter = _channelSourceGetterMap.GetOrAdd(owner.GetType(), t => VisibilityBypasser.Instance.GenerateFieldReadAccessor<object>(t, "_channelSource"));
            return getter(owner);
        }

        public static object GetDatabaseFromGeneric(object owner)
        {
            var getter = _databaseGetterMap.GetOrAdd(owner.GetType(), t => VisibilityBypasser.Instance.GeneratePropertyAccessor<object>(t, "Database"));
            return getter(owner);
        }

        private static object GetServerFromFromInterface(object owner)
        {
            var getter = _serverGetterMap.GetOrAdd(owner.GetType(), t => VisibilityBypasser.Instance.GeneratePropertyAccessor<object>(t, "Server"));
            return getter(owner);
        }

        public static ConnectionInfo GetConnectionInfoFromCursor(object asyncCursor, object collectionNamespace, string utilizationHostName)
        {
            string host = null;
            int port = -1;

            var channelSource = GetChannelSourceFieldFromGeneric(asyncCursor);
            var server = GetServerFromFromInterface(channelSource);
            EndPoint endpoint = GetEndPoint(server);

            var dnsEndpoint = endpoint as DnsEndPoint;
            var ipEndpoint = endpoint as IPEndPoint;

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

            var databaseName = GetDatabaseNameFromCollectionNamespace(collectionNamespace);

            return new ConnectionInfo(DatastoreVendor.MongoDB.ToKnownName(), host, port, databaseName);
        }

        public static ConnectionInfo GetConnectionInfoFromDatabase(object database, string utilizationHostName)
        {
            var databaseName = GetDatabaseNameFromDatabase(database);
            var servers = GetServersFromDatabase(database);

            int port = -1;
            string host = null;

            if (servers.Count == 1)
            {
                GetHostAndPortFromServer(servers[0], out var rawHost, out var rawPort);
                port = rawPort;
                host = ConnectionStringParserHelper.NormalizeHostname(rawHost, utilizationHostName);
            }

            return new ConnectionInfo(DatastoreVendor.MongoDB.ToKnownName(), host, port, databaseName);
        }

        private static IList GetServersFromDatabase(object database)
        {
            var clientGetter = _getClient ?? (_getClient = VisibilityBypasser.Instance.GeneratePropertyAccessor<object>("MongoDB.Driver", "MongoDB.Driver.MongoDatabaseImpl", "Client"));
            var settingsGetter = _getSettings ?? (_getSettings = VisibilityBypasser.Instance.GeneratePropertyAccessor<object>("MongoDB.Driver", "MongoDB.Driver.MongoClient", "Settings"));
            var serversGetter = _getServers ?? (_getServers = VisibilityBypasser.Instance.GenerateFieldReadAccessor<IList>("MongoDB.Driver", "MongoDB.Driver.MongoClientSettings", "_servers"));

            var client = clientGetter(database);
            var settings = settingsGetter(client);
            return serversGetter(settings);
        }

        private static void GetHostAndPortFromServer(object server, out string host, out int port)
        {
            var hostGetter = _getHost ?? (_getHost = VisibilityBypasser.Instance.GeneratePropertyAccessor<string>("MongoDB.Driver", "MongoDB.Driver.MongoServerAddress", "Host"));
            var portGetter = _getPort ?? (_getPort = VisibilityBypasser.Instance.GeneratePropertyAccessor<int>("MongoDB.Driver", "MongoDB.Driver.MongoServerAddress", "Port"));

            host = hostGetter(server);
            port = portGetter(server);
        }

    }
}
