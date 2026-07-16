// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Threading.Tasks;
using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Parsing;
using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Providers.Wrapper.MongoDb26;

public class MongoCollectionBaseWrapper : IWrapper
{
    private const string WrapperName = "MongoCollectionBaseWrapper";
    public bool IsTransactionRequired => true;

    public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
    {
        return new CanWrapResponse(WrapperName.Equals(methodInfo.RequestedWrapperName));

    }

    public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
    {
        var operation = instrumentedMethodCall.MethodCall.Method.MethodName;
        var caller = instrumentedMethodCall.MethodCall.InvocationTarget;

        var collectionNamespace = MongoDbHelper.GetCollectionNamespacePropertyFromGeneric(caller);
        var model = MongoDbHelper.GetCollectionName(collectionNamespace);

        var database = MongoDbHelper.GetDatabaseFromGeneric(caller);

        ConnectionInfo connectionInfo = MongoDbHelper.GetConnectionInfoFromDatabase(database, agent.Configuration.UtilizationHostName);
        agent.Logger.Info($"MongoDB connection info: Host={connectionInfo.Host}, Port={connectionInfo.PortPathOrId}, Database={connectionInfo.DatabaseName}, Instance={connectionInfo.InstanceName}");

        var segment = transaction.StartDatastoreSegment(instrumentedMethodCall.MethodCall,
            new ParsedSqlStatement(DatastoreVendor.MongoDB, model, operation), isLeaf: true, connectionInfo: connectionInfo);

        // POC - attempt to construct AWS cloud entity synthesis attibutes if we detect that this is a DocumentDB being queried.
        // example cluster hostname: docdb-sandbox.cluster-cokpc3aw6fev.us-west-2.docdb.amazonaws.com
        // The customer will need to supply their AWS account ID for this to work since it's not available anywhere in the connection

        //if (connectionInfo.Host.EndsWith("docdb.amazonaws.com") && !string.IsNullOrEmpty(agent.Configuration.AwsAccountId))
        //{
        //    var hostParts = connectionInfo.Host.Split('.');
        //    // Get the cluster id and region from the hostname
        //    var clusterId = hostParts[0];
        //    var region = hostParts[2];
        //    // construct the ARN
        //    var arn = $"arn:aws:rds:{region}:{agent.Configuration.AwsAccountId}:cluster:{clusterId}";
        //    // Add the necessary attributes to the datastore segment
        //    segment.AddCloudSdkAttribute("cloud.resource_id", arn);
        //    segment.AddCloudSdkAttribute("aws.operation", operation);
        //    segment.AddCloudSdkAttribute("aws.region", region);
        //    agent.Logger.Info($"Added cloud.resource_id (arn) to datastore segment: {arn}");
        //}

        if (!operation.EndsWith("Async", StringComparison.OrdinalIgnoreCase))
        {
            return Delegates.GetDelegateFor(segment);
        }

        return Delegates.GetAsyncDelegateFor<Task>(agent, segment, true);
    }

}
