// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using NewRelic.Agent.Api;
using NewRelic.Agent.Api.Experimental;
using NewRelic.Agent.Extensions.Parsing;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Agent.Extensions.SystemExtensions;
using NewRelic.Reflection;

namespace NewRelic.Agent.Extensions.Helpers
{
    public abstract class SearchRequestWrapperBase
    {
        private Func<object, object> _apiCallDetailsGetter;
        private Func<object, bool> _successGetter;
        private Func<object, object> _exceptionGetter;
        private Func<object, Uri> _uriGetter;

        protected ConcurrentDictionary<Type, Func<object, object>> GetRequestResponseFromGeneric = new ConcurrentDictionary<Type, Func<object, object>>();

        public abstract DatastoreVendor Vendor { get; } 

        protected ISegment BuildSegment(int requestParamsIndex, InstrumentedMethodCall instrumentedMethodCall, ITransaction transaction)
        {
            var path = (string)instrumentedMethodCall.MethodCall.MethodArguments[1];
            var request = instrumentedMethodCall.MethodCall.MethodArguments[0].ToString();
            var requestParams = instrumentedMethodCall.MethodCall.MethodArguments[requestParamsIndex];
            var splitPath = path.Trim('/').Split('/');

            var operation = (requestParams == null) ? GetOperationFromPath(request, splitPath) : GetOperationFromRequestParams(requestParams);

            var model = splitPath[0]; // For SQL datastores, "model" is the table name. For OpenSearch it's the index name.  This is often the first component of the request path, but not always.
            if ((model.Length == 0) || (model[0] == '_')) // Per OpenSearch docs, index names aren't allowed to start with an underscore, and the first component of the path can be an operation name in some cases, e.g. "_bulk" or "_msearch"
            {
                model = "Unknown";
            }

            var transactionExperimental = transaction.GetExperimentalApi();
            var datastoreSegmentData = transactionExperimental.CreateDatastoreSegmentData(new ParsedSqlStatement(Vendor, model, operation), new ConnectionInfo(string.Empty, string.Empty, string.Empty), string.Empty, null);
            var segment = transactionExperimental.StartSegment(instrumentedMethodCall.MethodCall);
            segment.GetExperimentalApi().SetSegmentData(datastoreSegmentData).MakeLeaf();

            return segment;
        }

        protected void TryProcessResponse(IAgent agent, ITransaction transaction, object response, ISegment segment)
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

        private void ReportError(ITransaction transaction, object apiCallDetails)
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
                transaction.NoticeError(new Exception(apiCallDetails.ToString()));
            }

        }

        private object GetApiCallDetailsFromResponse(object response)
        {
            var ApiCallDetailsGetter = _apiCallDetailsGetter ??= GetApiCallDetailsGetterFromResponse(response);
            var apiCallDetails = ApiCallDetailsGetter.Invoke(response);
            return apiCallDetails;
        }

        private Uri GetUriFromApiCallDetails(object apiCallDetails)
        {
            var UriGetter = _uriGetter ??= GetUriGetterFromApiCallDetails(apiCallDetails);
            var uri = UriGetter.Invoke(apiCallDetails);

            return uri;
        }

        private static Func<object, object> GetApiCallDetailsGetterFromResponse(object response)
        {
            var typeOfResponse = response.GetType();
            var responseAssemblyName = typeOfResponse.Assembly.FullName;
            var apiCallDetailsPropertyName =
                responseAssemblyName.StartsWith("OpenSearch.Net") || responseAssemblyName.StartsWith("Elastic.Clients.Elasticsearch")
                ? "ApiCallDetails" : "ApiCall";

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

        // Some request types are defined by the HTTP request
        private static ReadOnlyDictionary<string, string> RequestMap = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>
        {
            { "PUT|_doc", "Index" },
            { "POST|_doc", "Index" },
            { "GET|_doc", "Get" },
            { "HEAD|_doc", "Get" },
            { "DELETE|_doc", "Delete" },
        });

        // Some request types use abbreviations
        private static ReadOnlyDictionary<string, string> RenameMap = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>
        {
            { "_mget", "MultiGet" },
            { "_termvectors", "TermVectors" },
            { "_msearch", "MultiSearch" },
            { "_mtermvectors", "MultiTermVectors" },
            { "_field_caps", "FieldCapabilities" },
        });

        // Some request types have a subtype
        private static ReadOnlyDictionary<string, string> SubTypeMap = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>
        {
            { "_search|template", "SearchTemplate" },
            { "_msearch|template", "MultiSearchTemplate" },
            { "_render|template", "RenderSearchTemplate" },
            { "_search|scroll", "Scroll" },
        });

        // Some request types depend on the type, subtype, and HTTP request
        private static ReadOnlyDictionary<string, string> FullRequestTypeMap = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>
        {
            { "DELETE|_search|scroll", "ClearScroll" },
        });

        private static void ParsePath(string[] splitPath, out string api, out string subType)
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

        private static string GetOperationFromPath(string request, string[] splitPath)
        {
            ParsePath(splitPath, out string api, out string subType);

            // Since different operations are determined by different combinations of the path, combine the different
            // elements into a single string with a separator, so we can do a faster dictionary lookup
            string operation;
            string apiWithSub = api + "|" + subType;
            string apiWithRequest = request + "|" + api;
            string fullApi = apiWithRequest + "|" + subType;

            // Check from most-specific to least-specific special cases. Most will fall through to the default handler.
            if (FullRequestTypeMap.TryGetValue(fullApi, out operation))
            {
                return operation;
            }
            if (SubTypeMap.TryGetValue(apiWithSub, out operation))
            {
                return operation;
            }
            if (RequestMap.TryGetValue(apiWithRequest, out operation))
            {
                return operation;
            }
            if (RenameMap.TryGetValue(api, out operation))
            {
                return operation;
            }

            // Many request types are named exactly for their API call, like _search, _create, _search_shards
            return api.CapitalizeEachWord('_');
        }

        protected static string GetOperationFromRequestParams(object requestParams)
        {
            if (requestParams == null)
            {
                // Params will be null when the low-level client is used, fall back to a generic operation name
                return "Query";
            }
            var typeOfRequestParams = requestParams.GetType();

            var requestParamsTypeName = typeOfRequestParams.Name;  // IndexRequestParameters, SearchRequestParameters, etc
            return requestParamsTypeName.Remove(requestParamsTypeName.Length - "RequestParameters".Length);
        }

        private static void SetUriOnDatastoreSegment(ISegment segment, Uri uri)
        {
            var segmentExperimentalApi = segment.GetExperimentalApi();
            var data = segmentExperimentalApi.SegmentData as IDatastoreSegmentData;
            data.SetConnectionInfo(new ConnectionInfo(uri.Host, uri.Port, string.Empty));
            segmentExperimentalApi.SetSegmentData(data);
        }

        protected static bool ValidTaskResponse(Task response)
        {
            return response?.Status == TaskStatus.RanToCompletion;
        }
    }
}
