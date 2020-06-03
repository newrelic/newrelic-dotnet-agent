using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using NewRelic.Reflection;

namespace NewRelic.Providers.Wrapper.RestSharp
{
    public class RestSharpHelper
    {
        private const string RestSharpAssemblyName = "RestSharp";

        private static Func<object, string> _getParameterName;
        private static Func<object, object> _getParameterValue;
        private static Func<object, Enum> _getMethod;
        private static Func<object, object, Uri> _buildUri;

        private static ConcurrentDictionary<Type, Func<object, object>> _getRestResponseFromGeneric = new ConcurrentDictionary<Type, Func<object, object>>();
        private static ConcurrentDictionary<Type, Func<object, object>> _getHeadersFromInterface = new ConcurrentDictionary<Type, Func<object, object>>();
        private static ConcurrentDictionary<Type, Func<object, object>> _getListElementsFromGeneric = new ConcurrentDictionary<Type, Func<object, object>>();
        private static ConcurrentDictionary<Type, Func<object, object>> _getStatusCodeFromInterface = new ConcurrentDictionary<Type, Func<object, object>>();

        public static Func<object, Enum> GetMethod => _getMethod ?? (_getMethod = VisibilityBypasser.Instance.GeneratePropertyAccessor<Enum>(RestSharpAssemblyName, "RestSharp.RestRequest", "Method"));

        //RestSharp is not strongly signed so type load fails if reference directly for .NET Framework applications
        public static Func<object, object, Uri> BuildUri => _buildUri ?? (_buildUri = VisibilityBypasser.Instance.GenerateOneParameterMethodCaller<Uri>(RestSharpAssemblyName, "RestSharp.RestClient", "BuildUri", "RestSharp.IRestRequest"));

        public static List<KeyValuePair<string, string>> GetResponseHeaders(object restResponse)
        {
            var headersToReturn = new List<KeyValuePair<string, string>>();

            var restResponseHeaders = GetHeadersFromInterface(restResponse);

            var items = GetListElementsAsArray(restResponseHeaders);

            foreach (var parameter in items)
            {
                headersToReturn.Add(new KeyValuePair<string, string>(GetParameterName(parameter), GetParameterValue(parameter)));
            }

            return headersToReturn;
        }

        public static object GetRestResponse(object responseTask)
        {
            var getResponse = _getRestResponseFromGeneric.GetOrAdd(responseTask.GetType(), t => VisibilityBypasser.Instance.GeneratePropertyAccessor<object>(t, "Result"));
            return getResponse(responseTask);
        }

        public static int GetResponseStatusCode(object restResponse)
        {
            var getStatusCode = _getStatusCodeFromInterface.GetOrAdd(restResponse.GetType(), t => VisibilityBypasser.Instance.GeneratePropertyAccessor<object>(RestSharpAssemblyName, "RestSharp.RestResponse", "StatusCode"));
            return (int)getStatusCode(restResponse);
        }

        private static object[] GetListElementsAsArray(object owner)
        {
            var ownerType = owner.GetType();
            var getElementsInList = _getListElementsFromGeneric.GetOrAdd(ownerType, t => VisibilityBypasser.Instance.GenerateFieldReadAccessor<object>(ownerType, "_items"));
            return (object[])getElementsInList(owner);
        }

        private static string GetParameterName(object parameter)
        {
            var getParameterName = _getParameterName ?? (_getParameterName = VisibilityBypasser.Instance.GeneratePropertyAccessor<string>(RestSharpAssemblyName, "RestSharp.Parameter", "Name"));
            return getParameterName(parameter);
        }

        private static string GetParameterValue(object parameter)
        {
            var getParameterValue = _getParameterValue ?? (_getParameterValue = VisibilityBypasser.Instance.GeneratePropertyAccessor<object>(RestSharpAssemblyName, "RestSharp.Parameter", "Value"));
            return (string)getParameterValue(parameter);
        }

        private static object GetHeadersFromInterface(object restResponse)
        {
            var getHeaders = _getHeadersFromInterface.GetOrAdd(restResponse.GetType(), t => VisibilityBypasser.Instance.GeneratePropertyAccessor<object>(RestSharpAssemblyName, "RestSharp.RestResponse", "Headers"));
            return getHeaders(restResponse);
        }
    }
}
