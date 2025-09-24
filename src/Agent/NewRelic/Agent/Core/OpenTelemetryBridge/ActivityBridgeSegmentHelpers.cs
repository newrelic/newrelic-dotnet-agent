// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using NewRelic.Agent.Api;
using NewRelic.Agent.Api.Experimental;
using NewRelic.Agent.Core.Errors;
using NewRelic.Agent.Core.Metrics;
using NewRelic.Agent.Core.Segments;
using NewRelic.Agent.Core.Transactions;
using NewRelic.Agent.Extensions.AwsSdk;
using NewRelic.Agent.Extensions.Logging;
using NewRelic.Agent.Extensions.Parsing;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Agent.Extensions.SystemExtensions;

namespace NewRelic.Agent.Core.OpenTelemetryBridge;

public static class ActivityBridgeSegmentHelpers
{
    // TODO: Coalesce with NewRelic.Agent.Extensions.Providers.Wrapper.Statics.DefaultCaptureHeaders
    public static readonly string[] DefaultCaptureHeaders = ["Referer", "Accept", "Content-Length", "Host", "User-Agent"];

    public static void ProcessActivityTags(this ISegment segment, object originalActivity, IAgent agent, IErrorService errorService)
    {
        dynamic activity = originalActivity;
        int activityKind = (int)activity.Kind;
        string activityId = activity.Id;
        string displayName = activity.DisplayName;

        string activityLogPrefix = ActivityLogPrefixHelpers.ActivityLogPrefix(activityId, activityKind, displayName);

        Log.Debug($"{activityLogPrefix} has stopped.");

        var tags = ((IEnumerable<KeyValuePair<string, object>>)activity.TagObjects).ToDictionary(t => t.Key, t => t.Value);
        if (tags.Count == 0)
        {
            Log.Finest($"{activityLogPrefix} has no tags. Not adding tags to segment.");
            return;
        }

        // process tags only if the activity status is not Error
        if ((int)activity.Status != (int)ActivityStatusCode.Error)
        {
            // based on activity kind, create the appropriate segment data
            // remove all tags that are used in segment data creation
            switch (activityKind)
            {
                case (int)ActivityKind.Client:
                    // could be an http call or a database call, so need to look for specific tags to decide
                    // order is important because some activities have both tags, e.g. a database call that is also an HTTP call, like Elasticsearch
                    if (tags.TryGetAndRemoveTag<string>(["db.system.name", "db.system"], out var dbSystemName)) // it's a database call
                    {
                        ProcessClientDatabaseTags(segment, agent, activity, activityLogPrefix, tags, dbSystemName);
                    }
                    else if (tags.TryGetAndRemoveTag<string>(["rpc.system"], out var rpcSystem)) // it's an RPC client activity
                    {
                        ProcessRpcClientTags(segment, agent, errorService, tags, activityLogPrefix, rpcSystem);
                    }
                    else if (tags.TryGetAndRemoveTag<string>(["http.request.method", "http.method"], out var method)) // it's an HTTP call
                    {
                        ProcessClientExternalTags(segment, agent, tags, activityLogPrefix, method);
                    }
                    else
                    {
                        Log.Finest($"{activityLogPrefix} is missing required tags to determine whether it's an external or database activity.");
                    }

                    break;
                case (int)ActivityKind.Producer: // producer and consumer both create messaging segments
                case (int)ActivityKind.Consumer:
                    if (tags.TryGetAndRemoveTag<string>(["messaging.system"], out var messagingSystem))
                    {
                        ProcessProducerConsumerMessagingSystemTags(segment, agent, activity, activityLogPrefix, tags, (ActivityKind)activityKind, messagingSystem);
                    }
                    else
                    {
                        Log.Finest($"{activityLogPrefix} is missing required tag for messaging system. Not creating a MessagingSegmentData.");
                    }

                    break;
                case (int)ActivityKind.Server:
                    // rpc server activities also have http server activity tags so
                    // check first whether it's an RPC server activity -- note that rpc.system may not exist so check for grpc.method also. 
                    if (tags.TryGetTag<string>(["rpc.system", "grpc.method"], out _))
                    {
                        ProcessRpcServerTags(segment, agent, errorService, tags, activityLogPrefix);
                    }
                    else if (tags.TryGetAndRemoveTag<string>(["http.request.method", "http.method"], out var serverMethod)) // it's an HTTP server activity
                    {
                        ProcessHttpServerTags(segment, agent, tags, activityLogPrefix, serverMethod);
                    }
                    else
                    {
                        Log.Finest($"{activityLogPrefix} is missing required tags to determine the type of server activity.");
                    }

                    break;
                case (int)ActivityKind.Internal:
                default:
                    break; // don't do anything -- we'll use the existing SimpleSegmentData that was created when the segment was created
            }
        }
        else
        {
            Log.Debug($"{activityLogPrefix} has a Status of Error. Not processing tags.");
        }

        // add any tags left in the collection as custom attributes
        foreach (var tag in tags)
        {
            // TODO: We may not want to add all tags to the segment. We may want to filter out some tags, especially
            // the ones that we map to intrinsic or agent attributes.
            segment.AddCustomAttribute(tag.Key, tag.Value);
        }
    }

    private static void ProcessRpcClientTags(ISegment segment, IAgent agent, IErrorService errorService, Dictionary<string, object> tags, string activityLogPrefix, string rpcSystem)
    {
        tags.TryGetAndRemoveTag<int?>(["rpc.grpc.status_code"], out var statusCode);

        tags.TryGetAndRemoveTag<string>(["rpc.method"], out var method);
        tags.TryGetAndRemoveTag<string>(["rpc.service"], out var service);
        tags.TryGetAndRemoveTag<string>(["grpc.method"], out var grpcMethod);

        tags.TryGetAndRemoveTag<string>(["server.address", "network.peer.address"], out var host);
        tags.TryGetAndRemoveTag<int>(["server.port", "network.peer.port"], out var port);

        // TODO: Otel tracing spec says "component" should be set to the rpc system, but the grpc spec makes no mention of it.
        // TODO: ExternalSegmentData curently sets Component as an Intrinsic attribute on the span, with a value of _segmentData.TypeName (which ends up being `NewRelic.Agent.Core.OpenTelemetryBridge.ActivityBridge`)  with no override available. So there's no way for us to set the same attribute with a different value.
        //segment.AddCustomAttribute("component", rpcSystem);

        var path = BuildRpcPath(host, port, service, method, grpcMethod);
        Uri uri = new Uri(path);
        var externalSegmentData = new ExternalSegmentData(uri, method);

        if (statusCode.HasValue)
            externalSegmentData.SetHttpStatus(statusCode.Value);

        Log.Finest($"Created ExternalSegmentData for {activityLogPrefix}.");

        segment.GetExperimentalApi().SetSegmentData(externalSegmentData);

        // per spec, a non-zero status code must be recorded as an exception.
        // TODO: This behavior is supposed to be configurable by the customer but currently is not
        if (statusCode.HasValue && statusCode.Value != 0)
        {
            RecordGrpcException(segment, agent, errorService, statusCode.Value, path, activityLogPrefix);
        }
    }

    // TODO: RPC Server implementation is very preliminary; unable to test currently because asp.net core grpc server doesn't create activities with the expected tags
    private static void ProcessRpcServerTags(ISegment segment, IAgent agent, IErrorService errorService, Dictionary<string, object> tags, string activityLogPrefix)
    {
        if (segment is not IHybridAgentSegment hybridAgentSegment)
        {
            return; // TODO: this shouldn't be possible; don't think we need to check for it
        }

        tags.TryGetAndRemoveTag<string>(["rpc.system"], out var rpcSystem); // may not exist

        tags.TryGetAndRemoveTag<int?>(["rpc.grpc.status_code"], out var statusCode);

        tags.TryGetAndRemoveTag<string>(["rpc.method", "grpc.method"], out var grpcMethod);
        tags.TryGetAndRemoveTag<string>(["rpc.method", "rpc.method"], out var method);
        tags.TryGetAndRemoveTag<string>(["rpc.service", "rpc.service"], out var service);

        tags.TryGetAndRemoveTag<string>(["server.address", "network.peer.address"], out var host);
        tags.TryGetAndRemoveTag<int?>(["server.port", "network.peer.port"], out var port);

        // TODO: Otel tracing spec says "component" should be set to the rpc system, but the grpc spec makes no mention of it.
        // TODO: ExternalSegmentData curently sets Component as an Intrinsic attribute on the span, with a value of _segmentData.TypeName (which ends up being `NewRelic.Agent.Core.OpenTelemetryBridge.ActivityBridge`)  with no override available. So there's no way for us to set the same attribute with a different value.
        //segment.AddCustomAttribute("component", rpcSystem);

        var transaction = hybridAgentSegment.GetTransactionFromSegment();

        var path = BuildRpcPath(host, port, service, method, grpcMethod);
        transaction.SetUri(path);

        transaction.SetRequestMethod(method ?? grpcMethod ?? "Unknown"); // TODO: should we default to "Unknown" or leave it null?
        if (statusCode.HasValue)
            transaction.SetHttpResponseStatusCode(statusCode.Value);

        // per spec, a non-zero status code must be recorded as an exception.
        // TODO: This behavior is supposed to be configurable by the customer but currently is not
        if (statusCode.HasValue && statusCode.Value != 0)
        {
            RecordGrpcException(segment, agent, errorService, statusCode.Value, path, activityLogPrefix);
        }
    }

    private static string BuildRpcPath(string host, int? port, string service, string method, string grpcMethod)
    {
        // construct the path according to gRPC spec
        // if service is missing, grpcMethod is the full path
        return !string.IsNullOrEmpty(service) ? $"grpc://{host}:{port}/{service}/{method}" : $"grpc://{host}:{port}{grpcMethod}";
    }

    private static void RecordGrpcException(ISegment segment, IAgent agent, IErrorService errorService, int statusCode, string path, string activityLogPrefix)
    {
        if (segment is not IHybridAgentSegment hybridAgentSegment)
        {
            return; // TODO: this shouldn't be possible; don't think we need to check for it
        }

        var transaction = hybridAgentSegment.GetTransactionFromSegment();
        if (transaction is IHybridAgentTransaction internalTransaction)
        {
            var errorMessage = $"gRPC call to {path} failed with status code {statusCode}";
            var errorData = errorService.FromMessage(errorMessage, (IDictionary<string, object>)null, false);

            internalTransaction.NoticeErrorOnTransactionAndSegment(errorData, segment);
            Log.Debug($"{activityLogPrefix} recorded gRPC exception: {errorMessage}");
        }
    }

    private static void ProcessHttpServerTags(ISegment segment, IAgent agent, Dictionary<string, object> tags, string activityLogPrefix, string requestMethod)
    {
        // look for http.route first, then fall back to url.path. Prefer http.route because it may contain templated route information,
        // but it won't exist for an invalid request.
        if (!tags.TryGetAndRemoveTag<string>(["http.route"], out var path))
        {
            tags.TryGetAndRemoveTag<string>(["url.path"], out path);
        }

        // if path is empty, consider it the same as /. Otherwise strip leading / if it exists
        path = string.IsNullOrEmpty(path) || path.Equals("/") ? "ROOT" : path[0] == '/' ? path.Substring(1) : path;

        tags.TryGetAndRemoveTag<string>(["url.query"], out var query);
        tags.TryGetAndRemoveTag<int>(["http.response.status_code", "http.status_code"], out var statusCode);

        if (segment is not IHybridAgentSegment hybridAgentSegment)
        {
            return; // TODO: this shouldn't be possible; don't think we need to check for it
        }

        var transaction = hybridAgentSegment.GetTransactionFromSegment();


        transaction.SetRequestMethod(requestMethod);
        transaction.SetUri(path);
        transaction.SetHttpResponseStatusCode(statusCode);

        if (statusCode >= 400)
        {
            transaction.SetWebTransactionName(WebTransactionType.StatusCode, $"{statusCode}", TransactionNamePriority.StatusCode);
        }
        else
        {
            transaction.SetWebTransactionNameFromPath(WebTransactionType.Custom, path);
        }

        if (!string.IsNullOrEmpty(query))
        {
            // if query starts with ?, trim it off
            if (query.StartsWith("?"))
            {
                query = query.Substring(1);
            }

            // split the query string into key/value pairs
            var parameters = query.Split('&')
                .Select(part => part.Split('='))
                .Where(part => part.Length == 2)
                .ToDictionary(split => split[0], split => split[1]);

            transaction.SetRequestParameters(parameters);
        }

        // iterate the tags collection and extract all values from keys that start with "http.request.header."
        // convert headers to a dictionary with the prefix removed from the key
        var headerPrefix = "http.request.header.";
        var startIndex = headerPrefix.Length;
        var headersDict = tags
            .Where(t => t.Key.StartsWith(headerPrefix, StringComparison.OrdinalIgnoreCase) && t.Value is string)
            .ToDictionary(t => t.Key.Substring(startIndex), t => (string)t.Value, StringComparer.OrdinalIgnoreCase);

        if (headersDict.Any())
        {
            // Filter to only headers that actually exist if not allowing all, to avoid KeyNotFoundException (e.g. Referer missing).
            IEnumerable<string> captureKeys = agent.Configuration.AllowAllRequestHeaders
                ? headersDict.Keys
                : DefaultCaptureHeaders.Where(h => headersDict.ContainsKey(h));

            if (captureKeys.Any())
            {
                transaction.SetRequestHeaders(headersDict, captureKeys, (hd, keyVal) => hd[keyVal]);
            }
        }
    }

    /// <summary>
    /// Processes messaging system tags for producer and consumer activities, creating and configuring a <see
    /// cref="MessageBrokerSegmentData"/> instance based on the provided tags and activity kind.
    /// </summary>
    /// <remarks>This method translates the messaging system name into a vendor-specific format,
    /// extracts relevant metadata from the provided tags, and determines the operation and destination type. It
    /// creates a <see cref="MessageBrokerSegmentData"/> instance to represent the messaging operation and
    /// associates it with the provided segment. For consumer activities with a "deliver" operation, the transaction
    /// name is also set based on the destination type, vendor, and destination name.
    ///
    /// see https://opentelemetry.io/docs/specs/semconv/messaging/rabbitmq/
    /// see https://opentelemetry.io/docs/specs/semconv/messaging/kafka/
    /// see https://opentelemetry.io/docs/specs/semconv/registry/attributes/messaging/
    /// </remarks>
    /// <param name="segment">The segment representing the current operation, used to associate the processed data.</param>
    /// <param name="agent">The agent instance responsible for managing telemetry data.</param>
    /// <param name="activity">The dynamic activity object containing telemetry information.</param>
    /// <param name="activityLogPrefix">A prefix used for logging activity-related messages.</param>
    /// <param name="tags">A dictionary of tags containing metadata about the messaging system and operation.</param>
    /// <param name="activityKind">The kind of activity being processed, such as producer or consumer.</param>
    /// <param name="messagingSystem">The name of the messaging system (e.g., RabbitMQ, Kafka, AWS SQS).</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if the <paramref name="activityKind"/> is not supported for the specified messaging system.</exception>
    private static void ProcessProducerConsumerMessagingSystemTags(ISegment segment, IAgent agent, dynamic activity, string activityLogPrefix, Dictionary<string, object> tags, ActivityKind activityKind, string messagingSystem)
    {
        // translate the messaging system to a vendor name suitable for MessageBrokerSegmentData
        string vendor = messagingSystem switch
        {
            // TODO: consolidate all vendor names to some common enum or constant similar to DatastoreVendor
            "rabbitmq" => "RabbitMQ",
            "kafka" => "Kafka",
            "aws.sqs" => SqsHelper.VendorName,
            "aws.sns" => "SNS",
            "azure.servicebus" => "ServiceBus",
            _ => messagingSystem.CapitalizeEachWord() // default to capitalizing each word in the messaging system name
        };

        Log.Finest($"{activityLogPrefix} has a messaging.system tag with value {messagingSystem}. Mapping to vendor {vendor}.");

        // get the routing key - Look for both RabbitMQ and Kafka specific tags first, falling back to the generic 
        if (!tags.TryGetAndRemoveTag<string>(["messaging.rabbitmq.destination.routing_key",
                "messaging.kafka.message.key",
                "messaging.destination.name"], out var routingKey))
        {
            Log.Finest($"{activityLogPrefix} is missing required tag for routing_key. Not creating a MessageBrokerSegmentData.");
            return;
        }

        tags.TryGetAndRemoveTag<string>(["messaging.operation.name"], out var operationName);

        var operation = activityKind switch
        {
            // doesn't work for RabbitMQ, as there is no activity for purge operation. Might work for other messaging systems but is unverified.
            ActivityKind.Producer when operationName == "purge" => MessageBrokerAction.Purge,
            // TODO: consider supporting other Producer operations like "create", "delete", "process", "receive", etc.

            ActivityKind.Producer => MessageBrokerAction.Produce,
            ActivityKind.Consumer => MessageBrokerAction.Consume,

            _ => throw new ArgumentOutOfRangeException(nameof(activityKind), activityKind, "Unsupported activity kind for messaging system.")
        };

        // messaging.destination.temporary isn't a required tag, so if it's not present we can infer it from the routing key
        tags.TryGetAndRemoveTag<bool?>(["messaging.destination.temporary"], out var isTemporary);
        var destinationType = GetBrokerDestinationType(routingKey, isTemporary);
        var destinationName = ResolveDestinationName(destinationType, routingKey);

        tags.TryGetAndRemoveTag<string>(["server.address", "net.peer.name", "net.peer.ip"], out var serverAddress);
        tags.TryGetAndRemoveTag<int>(["server.port", "net.peer.port"], out var serverPort);

        var action = MetricNames.AgentWrapperApiEnumToMetricNamesEnum(operation);
        var destType = MetricNames.AgentWrapperApiEnumToMetricNamesEnum(destinationType);

        // create the segment data
        var segmentData = new MessageBrokerSegmentData(
            vendor,
            destinationName,
            destType,
            action,
            messagingSystemName: messagingSystem,
            serverAddress: serverAddress,
            serverPort: serverPort,
            routingKey: routingKey);

        Log.Finest($"Created MessageBrokerSegmentData for {activityLogPrefix}.");

        // set the transaction name if this is a consumer activity and the operation name is "deliver" (because we created the transaction)
        if (operation == MessageBrokerAction.Consume && operationName == "deliver")
        {

            if (segment is IHybridAgentSegment hybridAgentSegment)
            {
                Log.Finest($"{activityLogPrefix} is a consumer activity. Setting transaction name to {TransactionName.ForBrokerTransaction(destinationType, vendor, destinationName)}");
                var transaction = hybridAgentSegment.GetTransactionFromSegment();
                transaction.SetMessageBrokerTransactionName(destinationType, vendor, destinationName);
            }
        }

        segment.GetExperimentalApi().SetSegmentData(segmentData);
    }

    private const string TempQueuePrefix = "amq.";
    private static MessageBrokerDestinationType GetBrokerDestinationType(string queueNameOrRoutingKey, bool? isTemporary)
    {
        if ((isTemporary.HasValue && isTemporary.Value) || queueNameOrRoutingKey.StartsWith(TempQueuePrefix))
            return MessageBrokerDestinationType.TempQueue;

        return queueNameOrRoutingKey.Contains(".") ? MessageBrokerDestinationType.Topic : MessageBrokerDestinationType.Queue;
    }

    private static string ResolveDestinationName(MessageBrokerDestinationType destinationType, string queueNameOrRoutingKey)
    {
        return destinationType is MessageBrokerDestinationType.TempQueue or MessageBrokerDestinationType.TempTopic
            ? null
            : queueNameOrRoutingKey;
    }

    private static void ProcessClientExternalTags(ISegment segment, IAgent agent, Dictionary<string, object> tags, string activityLogPrefix, string method)
    {
        if (!tags.TryGetAndRemoveTag<string>(["url.full", "http.url"], out var url))
        {
            Log.Finest($"{activityLogPrefix} is missing url. Not creating an ExternalSegmentData.");
            return;
        }
        if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(method))
        {
            Log.Finest($"{activityLogPrefix} is missing required tags for url and method. Not creating an ExternalSegmentData.");
            return;
        }

        Uri uri = new Uri(url);
        var externalSegmentData = new ExternalSegmentData(uri, method);
        // set the http status code
        if (tags.TryGetAndRemoveTag<int>(["http.response.status_code", "http.status_code"], out var statusCode))
        {
            tags.TryGetAndRemoveTag<string>(["http.status_text"], out var statusText);
            externalSegmentData.SetHttpStatus(statusCode, statusText);
        }

        // check for AWS lambda invocation call
        if (!string.IsNullOrEmpty(agent.Configuration.AwsAccountId))
        {
            if (tags.TryGetAndRemoveTag<string>(["faas.invoked_name"], out var awsName))
                segment.AddCloudSdkAttribute("cloud.platform", "aws_lambda");
            if (tags.TryGetAndRemoveTag<string>(["faas.invoked_provider"], out var awsProvider) && tags.TryGetAndRemoveTag<string>(["aws.region"], out var awsRegion))
                segment.AddCloudSdkAttribute("cloud.resource_id", $"arn:aws:lambda:{awsRegion}:{agent.Configuration.AwsAccountId}:function:{awsName}");
        }
        Log.Finest($"Created ExternalSegmentData for {activityLogPrefix}.");

        segment.GetExperimentalApi().SetSegmentData(externalSegmentData);
    }

    private static void ProcessClientDatabaseTags(ISegment segment, IAgent agent, dynamic activity, string activityLogPrefix, Dictionary<string, object> tags, string dbSystemName)
    {
        DatastoreVendor vendor = dbSystemName switch
        {
            "couchdb" => DatastoreVendor.Couchbase,
            "oracle.db" => DatastoreVendor.Oracle,
            "microsoft.sql_server" => DatastoreVendor.MSSQL,
            "mongodb" => DatastoreVendor.MongoDB,
            "mysql" => DatastoreVendor.MySQL,
            "postgresql" => DatastoreVendor.Postgres,
            "redis" => DatastoreVendor.Redis,
            "azure.cosmosdb" => DatastoreVendor.CosmosDB,
            "elasticsearch" => DatastoreVendor.Elasticsearch,
            "aws.dynamodb" => DatastoreVendor.DynamoDB,
            _ => DatastoreVendor.Other
        };

        Log.Finest($"{activityLogPrefix} has db.system.name tag with value {dbSystemName}. Mapping to vendor {vendor}.");

        // get an appropriate datastore segment data based on the vendor
        ISegmentData segmentData = vendor switch
        {
            DatastoreVendor.Elasticsearch => GetElasticSearchDatastoreSegmentData(agent, tags, vendor, activityLogPrefix),
            _ => GetDefaultDatastoreSegmentData(agent, activity, activityLogPrefix, tags, vendor)
        };

        if (segmentData != null)
        {
            segment.GetExperimentalApi().SetSegmentData(segmentData);
        }
    }

    private static ISegmentData GetDefaultDatastoreSegmentData(IAgent agent, dynamic activity, string activityLogPrefix, Dictionary<string, object> tags, DatastoreVendor vendor)
    {
        // TODO: We may get two activities with "db.system" tags - one with a DisplayName of "Open" and one with a DisplayName of "Execute".
        // TODO: The "Execute" activity will have the SQL command text in the tags, while the "Open" activity will not.
        // TODO: What do we do with the "Open" activity? For now, we'll ignore it
        if (activity.DisplayName != "Execute")
            return null;

        tags.TryGetAndRemoveTag<string>(["db.query.text", "db.statement"], out var commandText);

        // TODO: Where do we get commandType? Existing SQL wrappers get it from the IDbCommand.CommandType property.
        var commandType = CommandType.Text;

        var parsedSqlStatement = SqlParser.GetParsedDatabaseStatement(vendor, commandType, commandText);

        tags.TryGetAndRemoveTag<string>(["server.address", "net.peer.name", "net.peer.ip"], out var serverAddress);
        tags.TryGetAndRemoveTag<int>(["server.port", "net.peer.port"], out var serverPort);
        tags.TryGetAndRemoveTag<string>(["db.namespace", "db.name"], out var databaseName);

        var connectionInfo = new ConnectionInfo(serverAddress, serverPort, databaseName);

        Log.Finest($"Created DatastoreSegmentData for {activityLogPrefix}");
        return new DatastoreSegmentData(agent.GetExperimentalApi().DatabaseService, parsedSqlStatement, commandText, connectionInfo);
    }

    private static ISegmentData GetElasticSearchDatastoreSegmentData(IAgent agent, Dictionary<string, object> tags, DatastoreVendor vendor, string activityLogPrefix)
    {
        tags.TryGetAndRemoveTag<string>(["db.operation.name", "db.operation"], out var operation);
        if (string.IsNullOrEmpty(operation))
        {
            Log.Finest($"Elasticsearch {activityLogPrefix} is missing required tag for operation. Not creating a DatastoreSegmentData.");
            return null;
        }
        operation = operation.CapitalizeEachWord('_'); // Normalize the operation name to be more consistent with other datastore operations.

        // model can be found in db.elasticsearch.path_parts.index if it exists, otherwise it can be found in the first component of the path.
        tags.TryGetAndRemoveTag<string>(["db.elasticsearch.path_parts.index"], out var model);
        if (!string.IsNullOrEmpty(model))
        {
            Log.Finest($"Elasticsearch {activityLogPrefix} has db.elasticsearch.path_parts.index tag with value {model}. Using it as the model.");
        }
        else
        {
            Log.Finest($"Elasticsearch {activityLogPrefix} is missing db.elasticsearch.path_parts.index tag. Using the first component of the path as the model.");

            if (!tags.TryGetAndRemoveTag<string>(["url.full", "http.url"], out var url))
            {
                Log.Finest($"Elasticsearch {activityLogPrefix} is missing `url.full` and `http.request.method`. Not creating a DatastoreSegmentData.");
                return null;
            }
            if (string.IsNullOrEmpty(url))
            {
                Log.Finest($"Elasticsearch {activityLogPrefix} is missing required tag for url. Not creating a DatastoreSegmentData.");
                return null;
            }

            Uri uri = new Uri(url);
            var splitPath = uri.AbsolutePath.Trim('/').Split('/');
            model = splitPath[0]; // For Elastic/OpenSearch model is the index name.  This is often the first component of the request path, but not always.
            if ((model.Length == 0) || (model[0] == '_')) // Per Elastic/OpenSearch docs, index names aren't allowed to start with an underscore, and the first component of the path can be an operation name in some cases, e.g. "_bulk" or "_msearch"
            {
                model = "Unknown";
            }
        }

        tags.TryGetAndRemoveTag<string>(["server.address", "net.peer.name", "net.peer.ip"], out var serverAddress);
        tags.TryGetAndRemoveTag<int>(["server.port", "net.peer.port"], out var serverPort);
        var connectionInfo = new ConnectionInfo(serverAddress, serverPort, string.Empty);

        var parsedSqlStatement = new ParsedSqlStatement(vendor, model, operation);

        Log.Finest($"Created DatastoreSegmentData for Elasticsearch {activityLogPrefix}");
        return new DatastoreSegmentData(agent.GetExperimentalApi().DatabaseService, parsedSqlStatement, string.Empty, connectionInfo);
    }

    public static void AddExceptionEventInformationToSegment(this ISegment segment, object originalActivity, IErrorService errorService)
    {
        // Exceptions recorded during an activity may be added as events on the activity. Not every way of recording
        // an exception will trigger the ExceptionRecorder callback, so we need to enumerate the events on the activity
        // to look for events with an eventName of "exception" and record the available exception information.

        dynamic activity = originalActivity;
        bool noticedError = false;
        foreach (var activityEvent in activity.Events)
        {
            if (activityEvent.Name == "exception")
            {
                string exceptionMessage = null;
                //string exceptionType = null;
                //string exceptionStacktrace = null;

                foreach (var tag in activityEvent.Tags)
                {
                    if (tag.Key == "exception.message")
                    {
                        exceptionMessage = tag.Value?.ToString();
                    }
                    //else if (tag.Key == "exception.type")
                    //{
                    //    exceptionType = tag.Value?.ToString();
                    //}
                    //else if (tag.Key == "exception.stacktrace")
                    //{
                    //    exceptionStacktrace = tag.Value?.ToString();
                    //}

                    // Add all of the original attributes to the segment.
                    segment.AddCustomAttribute((string)tag.Key, (object)tag.Value);
                }

                if (exceptionMessage != null)
                {

                    // TODO: The agent does not support ignoring errors by message, but if a type is available we could
                    // consider ignoring the error based on the type.

                    // TODO: In the future consider using the span status to determine if the exception is expected or not.
                    var errorData = errorService.FromMessage(exceptionMessage, (IDictionary<string, object>)null, false);
                    //var span = (IInternalSpan)segment;
                    //span.ErrorData = errorData;

                    // TODO: Record the errorData on the transaction.
                    if (segment is IHybridAgentSegment hybridAgentSegment)
                    {
                        var transaction = hybridAgentSegment.GetTransactionFromSegment();
                        if (transaction is IHybridAgentTransaction internalTransaction)
                        {
                            internalTransaction.NoticeErrorOnTransactionAndSegment(errorData, segment);
                            noticedError = true;
                        }
                    }
                }

                // Short circuiting the loop after finding the first exception event.
                break;
            }
        }

        // as a fallback, if no exception event was found, we can still check the activity status code.
        if (!noticedError)
        {
            if ((int)activity.Status == (int)ActivityStatusCode.Error)
            {
                // If no exception event was found, but the activity has an error status code, we can still record the error.
                // This is a fallback in case the activity does not have an "exception" event.

                // get the Tags and convert to a dictionary
                var tags = ((IEnumerable<KeyValuePair<string, object>>)activity.TagObjects).ToDictionary(t => t.Key, t => t.Value);
                var errorData = errorService.FromMessage(activity.StatusDescription ?? "Unknown error", (IDictionary<string, object>)tags, false);
                if (segment is IHybridAgentSegment hybridAgentSegment)
                {
                    var transaction = hybridAgentSegment.GetTransactionFromSegment();
                    if (transaction is IHybridAgentTransaction internalTransaction)
                    {
                        internalTransaction.NoticeErrorOnTransactionAndSegment(errorData, segment);
                    }
                }
            }
        }

    }
}

public static class ActivityLogPrefixHelpers
{
    public static string ActivityLogPrefix(string activityId, int activityKindInt, string activityDisplayName)
    {
        return $"Activity {activityId} (Kind: {(ActivityKind)activityKindInt}, DisplayName: {activityDisplayName})";
    }
}

public enum GrpcStatusCodes
{
    Ok = 0,
    Cancelled = 1,
    Unknown = 2,
    InvalidArgument = 3,
    DeadlineExceeded = 4,
    NotFound = 5,
    AlreadyExists = 6,
    PermissionDenied = 7,
    ResourceExhausted = 8,
    FailedPrecondition = 9,
    Aborted = 10,
    OutOfRange = 11,
    Unimplemented = 12,
    Internal = 13,
    Unavailable = 14,
    DataLoss = 15,
    Unauthenticated = 16
}
