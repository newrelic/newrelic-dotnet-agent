// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Parsing;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Reflection;

namespace NewRelic.Providers.Wrapper.Elasticsearch
{

    public class RequestWrapper : IWrapper
    {
        public bool IsTransactionRequired => true;

        private const string WrapperName = "RequestWrapper";

        private Func<object, object> _apiCallDetailsGetter;
        private Func<object, Uri> _uriGetter;

        public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
        {
            return new CanWrapResponse(WrapperName.Equals(methodInfo.RequestedWrapperName));

        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
        {
            //Elasticsearch.Net.HttpMethod,System.String,Elasticsearch.Net.PostData,Elasticsearch.Net.IRequestParameters
            var path = (string)instrumentedMethodCall.MethodCall.MethodArguments[1];
            var postData = instrumentedMethodCall.MethodCall.MethodArguments[2];
            var requestParams = instrumentedMethodCall.MethodCall.MethodArguments[3];

            //  For reference here's Elastic's take on mapping SQL terms/concepts to Elastic's: https://www.elastic.co/guide/en/elasticsearch/reference/current/_mapping_concepts_across_sql_and_elasticsearch.html
            var databaseName = string.Empty; // Per Elastic.co this SQL DB concept most closely maps to "cluster instance name".  TBD how to get this
            var model = path.Trim('/').Split('/')[0]; // "model"=table name for SQL.  For elastic it's index name.  It appears to always be the first component of the path

            var operation = GetOperationFromRequestParams(requestParams);

            Uri endpoint = null; // Unavailable at this point, but maybe available in the response - need to do some work in the AfterWrappedMethodDelegate

            // Playing with extending the experimental API to allow us to add data to a datastore segment after it has been started.
            // See ITransactionExperimental.CreateExternalSegmentData and how that is used in the SendAsync wrapper for HttpClient instrumentation.
            // Ran out of time during the spike to carry this through to completion.

            //var transactionExperimental = transaction.GetExperimentalApi();
            //var externalSegmentData = transactionExperimental.CreateDatastoreSegmentData(DatastoreVendor.Elasticsearch, model, operation, new ConnectionInfo(string.Empty, string.Empty, databaseName));
            //var segment = transactionExperimental.StartSegment(instrumentedMethodCall.MethodCall);
            //segment.GetExperimentalApi().SetSegmentData(externalSegmentData);

            var segment = transaction.StartDatastoreSegment(
                instrumentedMethodCall.MethodCall,
                new ParsedSqlStatement(DatastoreVendor.Elasticsearch, model, operation),
                connectionInfo: endpoint != null ? new ConnectionInfo(endpoint.Host, endpoint.Port.ToString(), databaseName) : new ConnectionInfo(string.Empty, string.Empty, databaseName),
                isLeaf: true);

            return Delegates.GetDelegateFor<object>(
                    onSuccess: response =>
                    {
                        var ApiCallDetailsGetter = _apiCallDetailsGetter ??= GetApiCallDetailsGetterFromResponse(response);
                        var apiCallDetails = ApiCallDetailsGetter.Invoke(response);

                        var UriGetter = _uriGetter ??= GetUriGetterFromResponse(response);
                        var uri = UriGetter.Invoke(apiCallDetails);

                        // TODO: need to figure out how to plumb things so that we can set the uri on the segment after it has already been created.
                        // See comments above about extending the experimental API

                        segment.End();
                    },
                    onFailure: exception =>
                    {
                        // Don't know how valid this is
                        segment.End(exception);
                    });
        }

        private string GetOperationFromRequestParams(object requestParams)
        {
            if (requestParams == null)
            {
                // Params will be null when the low-level Elasticsearch.Net client is used, fall back to a generic operation name
                return "Query";
            }
            var typeOfRequestParams = requestParams.GetType();

            var requestParamsTypeName = typeOfRequestParams.Name;  // IndexRequestParameters, SearchRequestParameters, etc
            return requestParamsTypeName.Remove(requestParamsTypeName.Length - "RequestParameters".Length);
        }

        private Func<object, object> GetApiCallDetailsGetterFromResponse(object response)
        {
            var typeOfResponse = response.GetType();
            var responseAssemblyName = typeOfResponse.Assembly.FullName;
            var apiCallDetailsPropertyName = responseAssemblyName.StartsWith("Elastic.Clients.Elasticsearch") ? "ApiCallDetails" : "ApiCall";

            return VisibilityBypasser.Instance.GeneratePropertyAccessor<object>(responseAssemblyName, typeOfResponse.FullName, apiCallDetailsPropertyName);
        }

        private Func<object, Uri> GetUriGetterFromResponse(object response)
        {
            var responseAssemblyName = response.GetType().Assembly.FullName;
            var apiCallDetailsAssemblyName = responseAssemblyName.StartsWith("Elastic.Clients.Elasticsearch") ? "Elastic.Transport" : "Elasticsearch.Net";
            var apiCallDetailsType = $"{apiCallDetailsAssemblyName}.ApiCallDetails";

            return VisibilityBypasser.Instance.GeneratePropertyAccessor<Uri>(apiCallDetailsAssemblyName, apiCallDetailsType, "Uri");
        }

    }
}
