// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Net;
using NewRelic.Agent.Extensions.Helpers;
using NewRelic.Agent.Extensions.Parsing;
using NewRelic.Agent.Extensions.Parsing.ConnectionString;
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

        private static readonly Version _mongo3Version = Version.Parse("3.0.0.0");
        private static Version _version;

        public static string GetCollectionName(object collectionNamespace)
        {
            _version ??= Version.Parse(VersionHelpers.GetLibraryVersion(collectionNamespace.GetType().AssemblyQualifiedName));
            var getter = _version >= _mongo3Version ? GetCollectionNameV3(collectionNamespace) : GetCollectionNameV2(collectionNamespace);
            return getter(collectionNamespace);
        }

        public static string GetDatabaseNameFromCollectionNamespace(object collectionNamespace)
        {
            _version ??= Version.Parse(VersionHelpers.GetLibraryVersion(collectionNamespace.GetType().AssemblyQualifiedName));
            var databaseNamespaceGetter = _version >= _mongo3Version ? GetDatabaseNameFromCollectionNamespaceV3(collectionNamespace) : GetDatabaseNameFromCollectionNamespaceV2(collectionNamespace);
            var databaseNamespace = databaseNamespaceGetter(collectionNamespace);

            return GetDatabaseNameFromDatabaseNamespace(databaseNamespace);
        }

        public static string GetDatabaseNameFromDatabase(object database)
        {
            _version ??= Version.Parse(VersionHelpers.GetLibraryVersion(database.GetType().AssemblyQualifiedName));
            var databaseNamespaceGetter = _version >= _mongo3Version ? GetDatabaseNameFromDatabaseV3(database) : GetDatabaseNameFromDatabaseV2(database);
            var databaseNamespace = databaseNamespaceGetter(database);

            return GetDatabaseNameFromDatabaseNamespace(databaseNamespace);
        }

        private static string GetDatabaseNameFromDatabaseNamespace(object databaseNamespace)
        {
            _version ??= Version.Parse(VersionHelpers.GetLibraryVersion(databaseNamespace.GetType().AssemblyQualifiedName));
            var databaseNameGetter = _version >= _mongo3Version ? GetDatabaseNameFromDatabaseNamespaceV3(databaseNamespace) : GetDatabaseNameFromDatabaseNamespaceV2(databaseNamespace);
            return databaseNameGetter(databaseNamespace);
        }

        private static EndPoint GetEndPoint(object owner)
        {
            _version ??= Version.Parse(VersionHelpers.GetLibraryVersion(owner.GetType().AssemblyQualifiedName));
            var getter = _version >= _mongo3Version ? GetEndPointV3(owner) : GetEndPointV2(owner);
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

            return new ConnectionInfo(host, port, databaseName);
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

            return new ConnectionInfo(host, port, databaseName);
        }

        private static IList GetServersFromDatabase(object database)
        {
            _version ??= Version.Parse(VersionHelpers.GetLibraryVersion(database.GetType().AssemblyQualifiedName));
            var clientGetter = _version >= _mongo3Version ? GetClientFromDatabaseV3(database) : GetClientFromDatabaseV2(database);
            var settingsGetter = _getSettings ??= VisibilityBypasser.Instance.GeneratePropertyAccessor<object>("MongoDB.Driver", "MongoDB.Driver.MongoClient", "Settings");
            var serversGetter = _getServers ??= VisibilityBypasser.Instance.GenerateFieldReadAccessor<IList>("MongoDB.Driver", "MongoDB.Driver.MongoClientSettings", "_servers");

            var client = clientGetter(database);
            var settings = settingsGetter(client);
            return serversGetter(settings);
        }

        private static void GetHostAndPortFromServer(object server, out string host, out int port)
        {
            var hostGetter = _getHost ??= VisibilityBypasser.Instance.GeneratePropertyAccessor<string>("MongoDB.Driver", "MongoDB.Driver.MongoServerAddress", "Host");
            var portGetter = _getPort ??= VisibilityBypasser.Instance.GeneratePropertyAccessor<int>("MongoDB.Driver", "MongoDB.Driver.MongoServerAddress", "Port");

            host = hostGetter(server);
            port = portGetter(server);
        }


        #region Client V2

        private static Func<object, string> GetCollectionNameV2(object collectionNamespace)
        {
            return _getCollectionName ??= VisibilityBypasser.Instance.GeneratePropertyAccessor<string>("MongoDB.Driver.Core", "MongoDB.Driver.CollectionNamespace", "CollectionName");
        }

        private static Func<object, object> GetDatabaseNameFromCollectionNamespaceV2(object collectionNamespace)
        {
            return _getDatabaseNamespaceFromCollectionNamespace ??= VisibilityBypasser.Instance.GeneratePropertyAccessor<object>("MongoDB.Driver.Core", "MongoDB.Driver.CollectionNamespace", "DatabaseNamespace");
        }

        private static Func<object, object> GetDatabaseNameFromDatabaseV2(object database)
        {
            return _getDatabaseNamespaceFromDatabase ??= VisibilityBypasser.Instance.GeneratePropertyAccessor<object>("MongoDB.Driver", "MongoDB.Driver.MongoDatabaseImpl", "DatabaseNamespace");
        }

        private static Func<object, string> GetDatabaseNameFromDatabaseNamespaceV2(object databaseNamespace)
        {
            return _getDatabaseName ??= VisibilityBypasser.Instance.GeneratePropertyAccessor<string>("MongoDB.Driver.Core", "MongoDB.Driver.DatabaseNamespace", "DatabaseName");
        }

        private static Func<object, EndPoint> GetEndPointV2(object owner)
        {
            return _getEndPoint ??= VisibilityBypasser.Instance.GeneratePropertyAccessor<EndPoint>("MongoDB.Driver.Core", "MongoDB.Driver.Core.Servers.Server", "EndPoint");
        }

        private static Func<object, object> GetClientFromDatabaseV2(object database)
        {
            return _getClient ??= VisibilityBypasser.Instance.GeneratePropertyAccessor<object>("MongoDB.Driver", "MongoDB.Driver.MongoDatabaseImpl", "Client");
        }

        #endregion

        #region Client V3

        private static Func<object, string> GetCollectionNameV3(object collectionNamespace)
        {
            return _getCollectionName ??= VisibilityBypasser.Instance.GeneratePropertyAccessor<string>("MongoDB.Driver", "MongoDB.Driver.CollectionNamespace", "CollectionName");
        }

        private static Func<object, object> GetDatabaseNameFromCollectionNamespaceV3(object collectionNamespace)
        {
            return _getDatabaseNamespaceFromCollectionNamespace ??= VisibilityBypasser.Instance.GeneratePropertyAccessor<object>("MongoDB.Driver", "MongoDB.Driver.CollectionNamespace", "DatabaseNamespace");
        }

        private static Func<object, object> GetDatabaseNameFromDatabaseV3(object database)
        {
            return _getDatabaseNamespaceFromDatabase ??= VisibilityBypasser.Instance.GeneratePropertyAccessor<object>("MongoDB.Driver", "MongoDB.Driver.MongoDatabase", "DatabaseNamespace");
        }

        private static Func<object, string> GetDatabaseNameFromDatabaseNamespaceV3(object databaseNamespace)
        {
            return _getDatabaseName ??= VisibilityBypasser.Instance.GeneratePropertyAccessor<string>("MongoDB.Driver", "MongoDB.Driver.DatabaseNamespace", "DatabaseName");
        }

        private static Func<object, EndPoint> GetEndPointV3(object owner)
        {
            return _getEndPoint ??= VisibilityBypasser.Instance.GeneratePropertyAccessor<EndPoint>("MongoDB.Driver", "MongoDB.Driver.Core.Servers.Server", "EndPoint");
        }

        private static Func<object, object> GetClientFromDatabaseV3(object database)
        {
            return _getClient ??= VisibilityBypasser.Instance.GeneratePropertyAccessor<object>("MongoDB.Driver", "MongoDB.Driver.MongoDatabase", "Client");
        }

        #endregion
    }
}
