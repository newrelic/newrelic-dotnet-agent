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
    public class RequestInvokerHandlerWrapper : IWrapper
    {
        public bool IsTransactionRequired => true;

        private const string AssemblyName = "Microsoft.Azure.Cosmos.Client";

        private const string RequestInvokerHandlerFullTypeName = "Microsoft.Azure.Cosmos.Handlers.RequestInvokerHandler";
        private const string CosmosClientFullTypeName = "Microsoft.Azure.Cosmos.CosmosClient";


        private Func<object, object> _getClient;
        private Func<object, Uri> _endpointGetter;

        private const string WrapperName = "RequestInvokerHandlerWrapper";


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

            var splittedAddressArray = resourceAddress.Split('/');
            var databaseName = string.Empty;
            var model = string.Empty;

            if (splittedAddressArray.Length > 1)
            {
                databaseName = string.IsNullOrEmpty(splittedAddressArray[1]) ? databaseName : splittedAddressArray[1];
            }

            if (splittedAddressArray.Length > 3)
            {
                model = string.IsNullOrEmpty(splittedAddressArray[3]) ? model : splittedAddressArray[3];
            }

            var operation = $"{operationType}{resourceType}";

            var clientGetter = _getClient ??= VisibilityBypasser.Instance.GenerateFieldReadAccessor<object>(AssemblyName, RequestInvokerHandlerFullTypeName, "client");

            var endpointGetter =  _endpointGetter ??= VisibilityBypasser.Instance.GeneratePropertyAccessor<Uri>(AssemblyName, CosmosClientFullTypeName, "Endpoint");

            var client = clientGetter.Invoke(instrumentedMethodCall.MethodCall.InvocationTarget);

            var endpoint = endpointGetter.Invoke(client);

            var segment = transaction.StartDatastoreSegment(
                instrumentedMethodCall.MethodCall,
                new ParsedSqlStatement(DatastoreVendor.CosmosDB, model, operation),
                connectionInfo: endpoint != null ? new ConnectionInfo(DatastoreVendor.CosmosDB.ToKnownName(), endpoint.Host, endpoint.Port, databaseName) : new ConnectionInfo(string.Empty, string.Empty, string.Empty, databaseName),
                isLeaf: true);

            return Delegates.GetAsyncDelegateFor<Task>(agent, segment);
        }
    }
}
