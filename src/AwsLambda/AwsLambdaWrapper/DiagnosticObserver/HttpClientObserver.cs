using NewRelic.OpenTracing.AmazonLambda.Helpers;
using OpenTracing;
using OpenTracing.Propagation;
using OpenTracing.Tag;
using OpenTracing.Util;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace NewRelic.OpenTracing.AmazonLambda.DiagnosticObserver
{
    /// <summary>
    /// HttpClientObserver to listen to HttpClient Start/Stop/Exception Events
    /// </summary>
    internal class HttpClientObserver : IObserver<KeyValuePair<string, object>>
    {
        // Use a specific tag to track Spans across Start/Stop
        private const string ScopeKey = "nr-ot-scope";

        private static readonly PropertyFetcher _start_RequestFetcher = new PropertyFetcher("Request");
        private static readonly PropertyFetcher _stop_RequestFetcher = new PropertyFetcher("Request");
        private static readonly PropertyFetcher _stop_ResponseFetcher = new PropertyFetcher("Response");
        private static readonly PropertyFetcher _stop_RequestTaskStatusFetcher = new PropertyFetcher("RequestTaskStatus");
        private static readonly PropertyFetcher _exception_RequestFetcher = new PropertyFetcher("Request");
        private static readonly PropertyFetcher _exception_ExceptionFetcher = new PropertyFetcher("Exception");

        public void OnCompleted()
        {
            // Perhaps this should be a no-op. Throwing an exception for now
            // to see if this gets invoked ever
            throw new NotImplementedException();
        }

        public void OnError(Exception error)
        {
            // Perhaps this should be a no-op. Throwing an exception for now
            // to see if this gets invoked ever
            throw new NotImplementedException();
        }

        /// <summary>
        /// OnNext is invoked on each HttpClient operation, the key is used
        /// to distinguish what event it is
        /// </summary>
        /// <param name="value"></param>
        public void OnNext(KeyValuePair<string, object> value)
        {
            switch (value.Key)
            {
                case "System.Net.Http.HttpRequestOut.Start":
                    {
                        ITracer tracer = GlobalTracer.Instance;
                        ISpan span = null;

                        var request = (HttpRequestMessage)_start_RequestFetcher.Fetch(value.Value);
                        // If host contains AWS try to parse specific requests
                        if (request.Headers.Host != null && request.Headers.Host.Contains(".amazonaws.com"))
                        {
                            span = AwsServiceHandler.CreateAWSSpans(request);
                        }
                        else if (span == null)
                        {
                            // Create a span and attach is as a property to the request
                            // so it can be completed when the request finishes.
                            span = tracer.BuildSpan(string.Format("External/{0}/{1}", request.RequestUri.Host, request.Method.Method)).Start();
                            span.SetTag(Tags.SpanKind, Tags.SpanKindClient);
                            span.SetTag(Tags.Component, "HttpOut");
                            span.SetTag(Tags.HttpMethod, request.Method.ToString());
                            span.SetTag(Tags.HttpUrl, request.RequestUri.ToString());
                            span.SetTag(Tags.PeerHostname, request.RequestUri.Host);
                            span.SetTag(Tags.PeerPort, request.RequestUri.Port);
                        }

                        if (span != null)
                        {
                            // create and use injector to set DT payload in headers.
                            var injector = new HttpRequestHeadersInjectAdapter(request.Headers);
                            tracer.Inject(span.Context, BuiltinFormats.HttpHeaders, injector);
                            request.Properties[ScopeKey] = span;
                        }
                    }

                    break;

                case "System.Net.Http.HttpRequestOut.Stop":
                    {
                        var request = (HttpRequestMessage)_stop_RequestFetcher.Fetch(value.Value);
                        if (request.Properties.TryGetValue(ScopeKey, out object objSpan) && objSpan is ISpan span)
                        {
                            var response = (HttpResponseMessage)_stop_ResponseFetcher.Fetch(value.Value);
                            var requestTaskStatus = (TaskStatus)_stop_RequestTaskStatusFetcher.Fetch(value.Value);
                            if (response != null)
                            {
                                span.SetTag(Tags.HttpStatus, (int)response.StatusCode);
                            }

                            if (requestTaskStatus == TaskStatus.Canceled || requestTaskStatus == TaskStatus.Faulted)
                            {
                                span.SetTag(Tags.Error, true);
                            }

                            span.Finish();
                            request.Properties[ScopeKey] = null;
                        }
                    }
                    break;

                case "System.Net.Http.Exception":
                    {
                        var request = (HttpRequestMessage)_exception_RequestFetcher.Fetch(value.Value);
                        // If the object has a span property add the error attributes
                        if (request.Properties.TryGetValue(ScopeKey, out object objSpan) && objSpan is ISpan span)
                        {
                            var exception = (Exception)_exception_ExceptionFetcher.Fetch(value.Value);
                            span.SetException(exception);
                        }
                    }

                    break;
            }
        }
    }
}
