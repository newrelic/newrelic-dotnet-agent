// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Threading.Tasks;
using NewRelic.Agent.Api;
using NewRelic.Agent.Api.Experimental;
using NewRelic.Agent.Extensions.Parsing;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Reflection;
using System.Collections.Concurrent;
using NewRelic.SystemExtensions;
using System.Collections.ObjectModel;
using System.Collections.Generic;

namespace NewRelic.Providers.Wrapper.Elasticsearch
{

    public class RequestWrapper : IWrapper
    {
        public bool IsTransactionRequired => true;

        private const string WrapperName = "RequestWrapper";

        private static Func<object, object> _apiCallDetailsGetter;
        private static Func<object, bool> _successGetter;
        private static Func<object, object> _exceptionGetter;
        private static Func<object, Uri> _uriGetter;
        
        private static ConcurrentDictionary<Type, Func<object, object>> _getRequestResponseFromGeneric = new ConcurrentDictionary<Type, Func<object, object>>();

        public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
        {
            return new CanWrapResponse(WrapperName.Equals(methodInfo.RequestedWrapperName));

        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
        {
            var indexOfRequestParams = 3; // unless it's Elasticsearch.Net/NEST and async

            if (instrumentedMethodCall.IsAsync)
            {
                transaction.AttachToAsync();

                var parameterTypeNamesList = instrumentedMethodCall.InstrumentedMethodInfo.Method.ParameterTypeNames.Split(',');
                if (parameterTypeNamesList[4] == "Elasticsearch.Net.IRequestParameters")
                {
                    indexOfRequestParams = 4;
                }
            }

            var path = (string)instrumentedMethodCall.MethodCall.MethodArguments[1];
            var request = instrumentedMethodCall.MethodCall.MethodArguments[0].ToString();
            var postData = instrumentedMethodCall.MethodCall.MethodArguments[2];
            var requestParams = instrumentedMethodCall.MethodCall.MethodArguments[indexOfRequestParams];

            var splitPath = path.Trim('/').Split('/');

            var operation = (requestParams == null) ? GetOperationFromPath(request, splitPath) : GetOperationFromRequestParams(requestParams);

            var model = splitPath[0]; // For SQL datastores, "model" is the table name. For Elastic it's the index name.  This is often the first component of the request path, but not always.
            if ((model.Length == 0) || (model[0] == '_')) // Per Elastic docs, index names aren't allowed to start with an underscore, and the first component of the path can be an operation name in some cases, e.g. "_bulk" or "_msearch"
            {
                model = "Unknown";
            }

            var transactionExperimental = transaction.GetExperimentalApi();
            var datastoreSegmentData = transactionExperimental.CreateDatastoreSegmentData(new ParsedSqlStatement(DatastoreVendor.Elasticsearch, model, operation), new ConnectionInfo(DatastoreVendor.Elasticsearch.ToKnownName(), string.Empty, string.Empty, string.Empty), string.Empty, null);
            var segment = transactionExperimental.StartSegment(instrumentedMethodCall.MethodCall);
            segment.GetExperimentalApi().SetSegmentData(datastoreSegmentData).MakeLeaf();

            if (instrumentedMethodCall.IsAsync)
            {
                return Delegates.GetAsyncDelegateFor<Task>(agent, segment, true, InvokeTryProcessResponse, TaskContinuationOptions.ExecuteSynchronously);

                void InvokeTryProcessResponse(Task responseTask)
                {
                    if (!ValidTaskResponse(responseTask) || (segment == null))
                    {
                        return;
                    }
                    var responseGetter = _getRequestResponseFromGeneric.GetOrAdd(responseTask.GetType(), t => VisibilityBypasser.Instance.GeneratePropertyAccessor<object>(t, "Result"));
                    var response = responseGetter(responseTask);
                    TryProcessResponse(agent, transaction, response, segment);
                }
            }
            else
            {
                return Delegates.GetDelegateFor<object>(
                        onSuccess: response =>
                        {
                            var apiCallDetails = GetApiCallDetailsFromResponse(response);
                            var uri = GetUriFromApiCallDetails(apiCallDetails);
                            SetUriOnDatastoreSegment(segment, uri);
                            ReportError(transaction, apiCallDetails);

                            segment.End();
                        },
                        onFailure: exception =>
                        {
                            // Don't know how valid this is
                            segment.End(exception);
                        });
            }
        }

        private static void ReportError(ITransaction transaction, object apiCallDetails)
        {
            var exceptionGetter = _exceptionGetter ??= GetExceptionGetterFromApiCallDetails(apiCallDetails);
            var ex = exceptionGetter(apiCallDetails);

            if ((ex != null) && (ex is Exception exception))
            {
                transaction.NoticeError(exception);
                return;
            }

            // If an error can be caught by the library before the request is made, it doesn't throw an exception, or
            // set any kind of error object. The best we can do is check if it was successful, and use the ToString()
            // override to get a summary of what happened
            var successGetter = _successGetter ??= GetSuccessGetterFromApiCallDetails(apiCallDetails);
            var success = successGetter(apiCallDetails);

            if (!success)
            {
                transaction.NoticeError(new ElasticsearchRequestException(apiCallDetails.ToString()));
            }

        }

        private static void TryProcessResponse(IAgent agent, ITransaction transaction, object response, ISegment segment)
        {
            try
            {
                if (response == null || segment == null)
                {
                    return;
                }
                var apiCallDetails = GetApiCallDetailsFromResponse(response);
                var uri = GetUriFromApiCallDetails(apiCallDetails);
                SetUriOnDatastoreSegment(segment, uri);
                ReportError(transaction, apiCallDetails);

                segment.End();
            }
            catch (Exception ex)
            {
                agent.HandleWrapperException(ex);
            }
        }

        private static object GetApiCallDetailsFromResponse(object response)
        {
            var ApiCallDetailsGetter = _apiCallDetailsGetter ??= GetApiCallDetailsGetterFromResponse(response);
            var apiCallDetails = ApiCallDetailsGetter.Invoke(response);
            return apiCallDetails;
        }

        private static Uri GetUriFromApiCallDetails(object apiCallDetails)
        {
            var UriGetter = _uriGetter ??= GetUriGetterFromApiCallDetails(apiCallDetails);
            var uri = UriGetter.Invoke(apiCallDetails);

            return uri;
        }

        private void ParsePath(string[] splitPath, out string api, out string subType)
        {
            // Some examples of different structures:
            // GET /my-index/_count?q=user:foo => API = "_count"
            // GET /my-index/_search => API = "_search"
            // PUT /my-index-000001 => API = ""
            // GET /_search/scroll => API = "_search", subType = "scroll"

            api = "";
            subType = "";
            bool foundApi = false;
            foreach (var path in splitPath)
            {
                if (string.IsNullOrEmpty(path))
                {
                    continue;
                }
                // Sub-api is directly after the API
                if (foundApi)
                {
                    subType = path.Split('?')[0];
                    break;
                }
                else if (path[0] == '_')
                {
                    // The API starts with an underscore and may have parameters
                    api = path.Split('?')[0];
                    foundApi = true;
                }
            }
        }

        // Some request types are defined by the HTTP request
        private static ReadOnlyDictionary<string, string> _requestMap = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>
        {
            { "PUT|_doc", "Index" },
            { "POST|_doc", "Index" },
            { "GET|_doc", "Get" },
            { "HEAD|_doc", "Get" },
            { "DELETE|_doc", "Delete" },
        });

        // Some request types use abbreviations
        private static ReadOnlyDictionary<string, string> _renameMap = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>
        {
            { "_mget", "MultiGet" },
            { "_termvectors", "TermVectors" },
            { "_msearch", "MultiSearch" },
            { "_mtermvectors", "MultiTermVectors" },
            { "_field_caps", "FieldCapabilities" },
        });

        // Some request types have a subtype
        private static ReadOnlyDictionary<string, string> _subTypeMap = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>
        {
            { "_search|template", "SearchTemplate" },
            { "_msearch|template", "MultiSearchTemplate" },
            { "_render|template", "RenderSearchTemplate" },
            { "_search|scroll", "Scroll" },
        });

        // Some request types depend on the type, subtype, and HTTP request
        private static ReadOnlyDictionary<string, string> _fullRequestTypeMap = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>
        {
            { "DELETE|_search|scroll", "ClearScroll" },
        });

        private string GetOperationFromPath(string request, string[] splitPath)
        {
            ParsePath(splitPath, out string api, out string subType);

            // Since different operations are determined by different combinations of the path, combine the different
            // elements into a single string with a separator, so we can do a faster dictionary lookup
            string operation;
            string apiWithSub = api + "|" + subType;
            string apiWithRequest = request + "|" + api;
            string fullApi = apiWithRequest + "|" + subType;

            // Check from most-specific to least-specific special cases. Most will fall through to the default handler.
            if (_fullRequestTypeMap.TryGetValue(fullApi, out operation))
            {
                return operation;
            }
            if (_subTypeMap.TryGetValue(apiWithSub, out operation))
            {
                return operation;
            }
            if (_requestMap.TryGetValue(apiWithRequest, out operation))
            {
                return operation;
            }
            if (_renameMap.TryGetValue(api, out operation))
            {
                return operation;
            }

            // Many request types are named exactly for their API call, like _search, _create, _search_shards
            return api.CapitalizeEachWord('_');
		}

        private static void SetUriOnDatastoreSegment(ISegment segment, Uri uri)
        {
            var segmentExperimentalApi = segment.GetExperimentalApi();
            var data = segmentExperimentalApi.SegmentData as IDatastoreSegmentData;
            data.SetConnectionInfo(new ConnectionInfo(DatastoreVendor.Elasticsearch.ToKnownName(), uri.Host, uri.Port, string.Empty));
            segmentExperimentalApi.SetSegmentData(data);
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

        private static Func<object, object> GetApiCallDetailsGetterFromResponse(object response)
        {
            var typeOfResponse = response.GetType();
            var responseAssemblyName = typeOfResponse.Assembly.FullName;
            var apiCallDetailsPropertyName = responseAssemblyName.StartsWith("Elastic.Clients.Elasticsearch") ? "ApiCallDetails" : "ApiCall";

            return VisibilityBypasser.Instance.GeneratePropertyAccessor<object>(responseAssemblyName, typeOfResponse.FullName, apiCallDetailsPropertyName);
        }

        private static Func<object, Uri> GetUriGetterFromApiCallDetails(object apiCallDetails)
        {
            var typeOfApiCall = apiCallDetails.GetType();
            var responseAssemblyName = apiCallDetails.GetType().Assembly.FullName;

            return VisibilityBypasser.Instance.GeneratePropertyAccessor<Uri>(responseAssemblyName, typeOfApiCall.FullName, "Uri");
        }

        private static Func<object, Exception> GetExceptionGetterFromApiCallDetails(object apiCallDetails)
        {
            var typeOfApiCall = apiCallDetails.GetType();
            var responseAssemblyName = apiCallDetails.GetType().Assembly.FullName;

            return VisibilityBypasser.Instance.GeneratePropertyAccessor<Exception>(responseAssemblyName, typeOfApiCall.FullName, "OriginalException");
        }

        private static Func<object, bool> GetSuccessGetterFromApiCallDetails(object apiCallDetails)
        {
            var typeOfApiCall = apiCallDetails.GetType();
            var responseAssemblyName = apiCallDetails.GetType().Assembly.FullName;

            // "Success" might be better, but it isn't available on all libraries
            return VisibilityBypasser.Instance.GeneratePropertyAccessor<bool>(responseAssemblyName, typeOfApiCall.FullName, "SuccessOrKnownError");
        }

        private static bool ValidTaskResponse(Task response)
        {
            return response?.Status == TaskStatus.RanToCompletion;
        }

    }
}
