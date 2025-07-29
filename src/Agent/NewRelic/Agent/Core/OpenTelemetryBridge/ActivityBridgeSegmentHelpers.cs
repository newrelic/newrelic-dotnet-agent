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
using NewRelic.Agent.Core.Segments;
using NewRelic.Agent.Core.Transactions;
using NewRelic.Agent.Extensions.Logging;
using NewRelic.Agent.Extensions.Parsing;
using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Agent.Core.OpenTelemetryBridge
{
    public static class ActivityBridgeSegmentHelpers
    {
        public static void AddActivityTagsToSegment(this ISegment segment, object originalActivity, IAgent agent)
        {
            dynamic activity = originalActivity;
            ActivityKind activityKind = (ActivityKind)activity.Kind;
            string activityId = activity.Id;
            string displayName = activity.DisplayName;

            Log.Debug($"Activity {activityId} (Kind: {activityKind}, DisplayName: {displayName}) has stopped.");

            var tags = ((IEnumerable<KeyValuePair<string, object>>)activity.TagObjects).ToDictionary(t => t.Key, t => t.Value);
            if (tags.Count == 0)
            {
                Log.Finest($"Activity {activityId} has no tags. Not adding tags to segment.");
                return;
            }

            // based on activity kind, create the appropriate segment data
            // remove all tags that are used in segment data creation
            switch (activityKind)
            {
                case ActivityKind.Client:
                    // could be an http call or a database call, so need to look for specific tags to decide
                    if (tags.TryGetAndRemoveTag<string>(["http.request.method", "http.method"], out var method)) // it's an HTTP call
                    {
                        ProcessClientExternalTags(segment, agent, tags, activityId, activityKind, method);
                    }
                    else if (tags.TryGetAndRemoveTag<string>(["db.system.name", "db.system"], out var dbSystemName)) // it's a database call
                    {
                        ProcessClientDatabaseTags(segment, agent, activity, tags, dbSystemName);
                    }
                    else
                    {
                        Log.Finest($"Activity {activityId} with Activity.Kind {activityKind} is missing required tags to determine whether it's an HTTP or database activity.");
                    }
                    break;
                case ActivityKind.Internal:
                case ActivityKind.Server:
                case ActivityKind.Producer:
                case ActivityKind.Consumer:
                default:
                    break;
            }

            // add any tags left in the collection as custom attributes
            foreach (var tag in tags)
            {
                // TODO: We may not want to add all tags to the segment. We may want to filter out some tags, especially
                // the ones that we map to intrinsic or agent attributes.
                segment.AddCustomAttribute(tag.Key, tag.Value);
            }

        }

        private static void ProcessClientExternalTags(ISegment segment, IAgent agent, Dictionary<string, object> tags, string activityId,
            ActivityKind activityKind, string method)
        {
            if (!tags.TryGetAndRemoveTag<string>(["url.full", "http.url"], out var url))
            {
                Log.Finest($"Activity {activityId} with Activity.Kind {activityKind} is missing `url.full` and `http.request.method`. Not creating an ExternalSegmentData.");
                return;
            }
            if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(method))
            {
                Log.Finest($"Activity {activityId} with Activity.Kind {activityKind} is missing required tags for url and method. Not creating an ExternalSegmentData.");
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

            segment.GetExperimentalApi().SetSegmentData(externalSegmentData);
        }

        private static void ProcessClientDatabaseTags(ISegment segment, IAgent agent, dynamic activity, Dictionary<string, object> tags, string dbSystemName)
        {
            // TODO: We may get two activities with "db.system" tags - one with a DisplayName of "Open" and one with a DisplayName of "Execute".
            // The "Execute" activity will have the SQL command text in the tags, while the "Open" activity will not.
            // What do we do with the "Open" activity? For now, we'll ignore it
            if (activity.DisplayName != "Execute")
                return;

            tags.TryGetAndRemoveTag<string>(["db.query.text", "db.statement"], out var commandText);

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

            // TODO: Where do we get commandType? Existing SQL wrappers get it from the IDbCommand.CommandType property.
            var commandType = CommandType.Text;

            var parsedSqlStatement = SqlParser.GetParsedDatabaseStatement(vendor, commandType, commandText);

            tags.TryGetAndRemoveTag<string>(["server.address", "net.peer.name", "net.peer.ip"], out var serverAddress);
            tags.TryGetAndRemoveTag<int>(["server.port", "net.peer.port"], out var serverPort);
            tags.TryGetAndRemoveTag<string>(["db.namespace", "db.name"], out var databaseName);

            var connectionInfo = new ConnectionInfo(serverAddress, serverPort, databaseName);

            var dbSegmentData = new DatastoreSegmentData(agent.GetExperimentalApi().DatabaseService, parsedSqlStatement, commandText, connectionInfo);

            segment.GetExperimentalApi().SetSegmentData(dbSegmentData);
        }

        public static void AddExceptionEventInformationToSegment(this ISegment segment, object originalActivity, IErrorService errorService)
        {
            // Exceptions recorded during an activity are currently added as events on the activity. Not every way of recording
            // an exception will trigger the ExceptionRecorder callback, so we need to enumerate the events on the activity
            // to look for events with an eventName of "exception" and record the available exception information.

            dynamic activity = originalActivity;
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
                            }
                        }
                    }

                    // Short circuiting the loop after finding the first exception event.
                    return;
                }
            }
        }
    }
}
