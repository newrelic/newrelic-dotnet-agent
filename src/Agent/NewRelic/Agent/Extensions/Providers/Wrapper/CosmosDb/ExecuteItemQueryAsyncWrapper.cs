// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Parsing;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Reflection;
using System.Threading.Tasks;

namespace NewRelic.Providers.Wrapper.CosmosDb
{
    public class ExecuteItemQueryAsyncWrapper : IWrapper
    {
        public bool IsTransactionRequired => true;

        private const string AssemblyName = "Microsoft.Azure.Cosmos.Client";

        private const string ClientContextCoreFullTypeName = "Microsoft.Azure.Cosmos.ClientContextCore";
        private const string CosmosClientFullTypeName = "Microsoft.Azure.Cosmos.CosmosClient";
        private const string CosmosQueryClientCoreFullTypeName = "Microsoft.Azure.Cosmos.CosmosQueryClientCore";
        private const string SqlQuerySpecFullTypeName = "Microsoft.Azure.Cosmos.Query.Core.SqlQuerySpec";


        private Func<object, object> _getClient;
        private Func<object, Uri> _endpointGetter;
        private Func<object, object> _getClientContextGetter;
        private Func<object, string> _queryGetter;
        private const string WrapperName = "ExecuteItemQueryAsyncWrapper";


        public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
        {
            return new CanWrapResponse(WrapperName.Equals(methodInfo.RequestedWrapperName));

        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
        {
            transaction.AttachToAsync();
        
            string resourceAddress = instrumentedMethodCall.MethodCall.MethodArguments[0].ToString();
            object resourceType = instrumentedMethodCall.MethodCall.MethodArguments[1].ToString();
            object operationType = instrumentedMethodCall.MethodCall.MethodArguments[2].ToString();

            object querySpec = instrumentedMethodCall.MethodCall.MethodArguments[6];

            var splitAddressArray = resourceAddress.Split('/');
            var databaseName = string.Empty;
            var model = string.Empty;

            if (splitAddressArray.Length > 1)
            {
                databaseName = string.IsNullOrEmpty(splitAddressArray[1]) ? databaseName : splitAddressArray[1];
            }

            if (splitAddressArray.Length > 3)
            {
                model = string.IsNullOrEmpty(splitAddressArray[3]) ? model : splitAddressArray[3];
            }

            var operation = $"{operationType}{resourceType}";

            var queryGetter = _queryGetter ??= VisibilityBypasser.Instance.GeneratePropertyAccessor<string>(AssemblyName, SqlQuerySpecFullTypeName, "QueryText");

            var clientContextGetter = _getClientContextGetter ??= VisibilityBypasser.Instance.GenerateFieldReadAccessor<object>(AssemblyName, CosmosQueryClientCoreFullTypeName, "clientContext");

            var clientGetter = _getClient ??= VisibilityBypasser.Instance.GenerateFieldReadAccessor<object>(AssemblyName, ClientContextCoreFullTypeName, "client");

            var endpointGetter =  _endpointGetter ??= VisibilityBypasser.Instance.GeneratePropertyAccessor<Uri>(AssemblyName, CosmosClientFullTypeName, "Endpoint");

            var clientContext = clientContextGetter.Invoke(instrumentedMethodCall.MethodCall.InvocationTarget);

            var client = clientGetter.Invoke(clientContext);

            var endpoint = endpointGetter.Invoke(client);

            var segment = transaction.StartDatastoreSegment(
                instrumentedMethodCall.MethodCall,
                new ParsedSqlStatement(DatastoreVendor.CosmosDB, model, operation),
                connectionInfo: endpoint != null ? new ConnectionInfo(DatastoreVendor.CosmosDB.ToKnownName(), endpoint.Host, endpoint.Port, databaseName) : new ConnectionInfo(string.Empty, string.Empty, string.Empty, databaseName),
                commandText : querySpec != null ? _queryGetter.Invoke(querySpec) : string.Empty,
                isLeaf: true);

            return Delegates.GetAsyncDelegateFor<Task>(agent, segment);
        }
    }
}
