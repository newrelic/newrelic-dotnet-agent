// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading.Tasks;
using ICSharpCode.SharpZipLib.Tar;
using NewRelic.Agent.Api;
using NewRelic.Agent.Core.Segments;
using NewRelic.Agent.Core.Transactions;
using NewRelic.Agent.Extensions.Helpers;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NUnit.Framework;
using Telerik.JustMock.AutoMock.Ninject.Activation;

namespace CompositeTests
{
    [TestFixture]
    public class SearchRequestWrapperBaseTests
    {
        private static CompositeTestAgent _compositeTestAgent;

        private IAgent _agent;

        private string _testHost = "localhost";
        private int _testPort = 1337;

        [SetUp]
        public void SetUp()
        {
            _compositeTestAgent = new CompositeTestAgent();
            _agent = _compositeTestAgent.GetAgent();
        }

        [TearDown]
        public static void TearDown()
        {
            _compositeTestAgent.Dispose();
        }

        [TestCase("PUT", "my-index", "_search", "Search", false, DatastoreVendor.OpenSearch)]
        [TestCase("GET", "search", "scroll", "Scroll", false, DatastoreVendor.OpenSearch)]
        [TestCase("PUT", "my-index", "_search", null, false, DatastoreVendor.OpenSearch)]
        [TestCase("PUT", "my-index", "/third", null, false, DatastoreVendor.OpenSearch)]
        [TestCase("GET", "search", "scroll", null, false, DatastoreVendor.OpenSearch)]

        [TestCase("PUT", "_my-index", "_search", "Search", false, DatastoreVendor.OpenSearch)]
        [TestCase("GET", "_search", "scroll", "Scroll", false, DatastoreVendor.OpenSearch)]
        [TestCase("PUT", "_my-index", "_search", null, false, DatastoreVendor.OpenSearch)]
        [TestCase("PUT", "_my-index", "/third", null, false, DatastoreVendor.OpenSearch)]
        [TestCase("GET", "_search", "scroll", null, false, DatastoreVendor.OpenSearch)]

        [TestCase("PUT", null, null, "Search", false, DatastoreVendor.OpenSearch)]
        public void Test_BuildSegment_TryProcessResponse_Success(string request, string path, string operationData,
            string requestParams, bool isAsync, DatastoreVendor datastoreVendor)
        {
            var testResponse = new TestSearchResponse(true, _testHost, _testPort, false);

            var combinedPath = String.IsNullOrEmpty(path) ? "" : $"/{path}/{operationData}";

            var testWrapper = new TestSearchRequestWrapper(datastoreVendor);
            var testRequest = new TestSearchRequest();
            var methodArgs = isAsync
                ? new object[] { request, combinedPath, null, null, GetRequestParams(requestParams) }
                : new object[] { request, combinedPath, null, GetRequestParams(requestParams) };

            var paramsIndex = isAsync ? 4 : 3;

            var method = new Method(typeof(TestSearchRequest), "", "");
            var methodCall = new MethodCall(method, testRequest, methodArgs, isAsync);
            var instrumentedMethodInfo = new InstrumentedMethodInfo(
                1,
                method,
                "requestedWrapperName",
                isAsync,
                "metricName",
                TransactionNamePriority.CustomTransactionName,
                false);

            var instrumentedMethodCall = new InstrumentedMethodCall(methodCall, instrumentedMethodInfo);

            var transaction = _agent.CreateTransaction(true, "mycategory", "mytransactionname", false);

            var segment = testWrapper.BuildSegment(paramsIndex, instrumentedMethodCall, transaction) as Segment;
            testWrapper.TryProcessResponse(_agent, transaction, testResponse, segment);

            Assert.That(segment, Is.Not.Null);
            Assert.That(segment.SegmentData, Is.Not.Null);

            var datastoreSegmentData = segment.SegmentData as DatastoreSegmentData;
            Assert.That(datastoreSegmentData, Is.Not.Null);
            Assert.That(datastoreSegmentData.DatastoreVendorName, Is.EqualTo(datastoreVendor));
            if (string.IsNullOrEmpty(path) || path.StartsWith("_"))
            {
                Assert.That(datastoreSegmentData.Model, Is.EqualTo("Unknown"));
            }
            else
            {
                Assert.That(datastoreSegmentData.Model, Is.EqualTo(path));
            }
            
            Assert.That(datastoreSegmentData.Operation, Is.Not.Null);
            Assert.That(datastoreSegmentData.Host, Is.EqualTo(_testHost));
            Assert.That(datastoreSegmentData.Port, Is.EqualTo(_testPort));
        }


        [TestCase("PUT", "my-index", "_search", "Search", false, DatastoreVendor.OpenSearch)]
        public void TryProcessResponse_HandlesException(string request, string path, string operationData,
            string requestParams, bool isAsync, DatastoreVendor datastoreVendor)
        {
            var testResponse = new EvilTestSearchResponse();

            var combinedPath = $"/{path}/{operationData}";

            var testWrapper = new TestSearchRequestWrapper(datastoreVendor);
            var testRequest = new TestSearchRequest();
            var methodArgs = isAsync
                ? new object[] { request, combinedPath, null, null, GetRequestParams(requestParams) }
                : new object[] { request, combinedPath, null, GetRequestParams(requestParams) };

            var paramsIndex = isAsync ? 4 : 3;

            var method = new Method(typeof(TestSearchRequest), "", "");
            var methodCall = new MethodCall(method, testRequest, methodArgs, isAsync);
            var instrumentedMethodInfo = new InstrumentedMethodInfo(
                1,
                method,
                "requestedWrapperName",
                isAsync,
                "metricName",
                TransactionNamePriority.CustomTransactionName,
                false);

            var instrumentedMethodCall = new InstrumentedMethodCall(methodCall, instrumentedMethodInfo);

            var transaction = _agent.CreateTransaction(true, "mycategory", "mytransactionname", false);

            var segment = testWrapper.BuildSegment(paramsIndex, instrumentedMethodCall, transaction) as Segment;
            testWrapper.TryProcessResponse(_agent, transaction, testResponse, segment);

            Assert.That(segment, Is.Not.Null);
            Assert.That(segment.SegmentData, Is.Not.Null);

            var datastoreSegmentData = segment.SegmentData as DatastoreSegmentData;
            Assert.That(datastoreSegmentData, Is.Not.Null);
            Assert.That(datastoreSegmentData.DatastoreVendorName, Is.EqualTo(datastoreVendor));
            if (path.StartsWith("_"))
            {
                Assert.That(datastoreSegmentData.Model, Is.EqualTo("Unknown"));
            }
            else
            {
                Assert.That(datastoreSegmentData.Model, Is.EqualTo(path));
            }

            Assert.That(datastoreSegmentData.Operation, Is.Not.Null);
            Assert.That(datastoreSegmentData.Host, Is.EqualTo("unknown"));
            Assert.That(datastoreSegmentData.Port, Is.Null);
        }


        [TestCase(true, true)]
        [TestCase(false, false)]
        [TestCase(true, false)]
        [TestCase(false, true)]
        public void Test_TryProcessResponse_Nulls(bool responseNull, bool segmentNull)
        {
            var testResponse = responseNull ? null : new TestSearchResponse(true, _testHost, _testPort, false);

            var combinedPath = $"/my-index/_search";

            var testWrapper = new TestSearchRequestWrapper(DatastoreVendor.Elasticsearch);
            var testRequest = new TestSearchRequest();
            var methodArgs = new object[] { "PUT", combinedPath, null, GetRequestParams("Search") };

            var paramsIndex = 3;

            var method = new Method(typeof(TestSearchRequest), "", "");
            var methodCall = new MethodCall(method, testRequest, methodArgs, false);
            var instrumentedMethodInfo = new InstrumentedMethodInfo(
                1,
                method,
                "requestedWrapperName",
                false,
                "metricName",
                TransactionNamePriority.CustomTransactionName,
                false);

            var instrumentedMethodCall = new InstrumentedMethodCall(methodCall, instrumentedMethodInfo);

            var transaction = _agent.CreateTransaction(true, "mycategory", "mytransactionname", false);

            var segment = segmentNull ? null : testWrapper.BuildSegment(paramsIndex, instrumentedMethodCall, transaction) as Segment;

            testWrapper.TryProcessResponse(_agent, transaction, testResponse, segment);

            if (responseNull && !segmentNull)
            {
                var segmentData = segment.SegmentData as DatastoreSegmentData;
                Assert.That(segmentData.Host, Is.Not.EqualTo(_testHost));
                Assert.That(segmentData.Port, Is.Not.EqualTo(_testPort));
            }
            else if(!responseNull && !segmentNull)
            {
                var segmentData = segment.SegmentData as DatastoreSegmentData;
                Assert.That(segmentData.Host, Is.EqualTo(_testHost));
                Assert.That(segmentData.Port, Is.EqualTo(_testPort));
            }
            else
            {
                // Nothing really to test if the segment is null.
                Assert.That(!transaction.IsFinished);
            }
        }

        [Test]
        public void ReportError_NoticeError()
        {
            var testResponse = new TestSearchResponse(false, _testHost, _testPort, true);

            var combinedPath = $"/my-index/_search";

            var testWrapper = new TestSearchRequestWrapper(DatastoreVendor.Elasticsearch);
            var testRequest = new TestSearchRequest();
            var methodArgs = new object[] { "PUT", combinedPath, null, GetRequestParams("Search") };

            var paramsIndex = 3;

            var method = new Method(typeof(TestSearchRequest), "", "");
            var methodCall = new MethodCall(method, testRequest, methodArgs, false);
            var instrumentedMethodInfo = new InstrumentedMethodInfo(
                1,
                method,
                "requestedWrapperName",
                false,
                "metricName",
                TransactionNamePriority.CustomTransactionName,
                false);

            var instrumentedMethodCall = new InstrumentedMethodCall(methodCall, instrumentedMethodInfo);

            var transaction = _agent.CreateTransaction(true, "mycategory", "mytransactionname", false);

            var segment = testWrapper.BuildSegment(paramsIndex, instrumentedMethodCall, transaction) as Segment;

            testWrapper.TryProcessResponse(_agent, transaction, testResponse, segment);

            var fullTransaction = transaction as Transaction;

            var errorState = fullTransaction.TransactionMetadata.TransactionErrorState;
            Assert.That(errorState.HasError);
            Assert.That(errorState.ErrorData.RawException.Message, Is.EqualTo("OriginalException"));
        }

        [Test]
        public void ReportError_SuccessGetter()
        {
            var testResponse = new TestSearchResponse(false, _testHost, _testPort, false);

            var combinedPath = $"/my-index/_search";

            var testWrapper = new TestSearchRequestWrapper(DatastoreVendor.Elasticsearch);
            var testRequest = new TestSearchRequest();
            var methodArgs = new object[] { "PUT", combinedPath, null, GetRequestParams("Search") };

            var paramsIndex = 3;

            var method = new Method(typeof(TestSearchRequest), "", "");
            var methodCall = new MethodCall(method, testRequest, methodArgs, false);
            var instrumentedMethodInfo = new InstrumentedMethodInfo(
                1,
                method,
                "requestedWrapperName",
                false,
                "metricName",
                TransactionNamePriority.CustomTransactionName,
                false);

            var instrumentedMethodCall = new InstrumentedMethodCall(methodCall, instrumentedMethodInfo);

            var transaction = _agent.CreateTransaction(true, "mycategory", "mytransactionname", false);

            var segment = testWrapper.BuildSegment(paramsIndex, instrumentedMethodCall, transaction) as Segment;

            testWrapper.TryProcessResponse(_agent, transaction, testResponse, segment);

            var fullTransaction = transaction as Transaction;

            var errorState = fullTransaction.TransactionMetadata.TransactionErrorState;
            Assert.That(errorState.HasError);
            Assert.That(errorState.ErrorData.RawException.Message, Is.EqualTo("CompositeTests.TestSearchResponseApiInfo"));
        }

        [Test]
        public void GetRequestResponseFromGeneric()
        {
            var request = new TestSearchRequest();
            var responseTask = Task.FromResult(request);
            var wrapperBase = new TestSearchRequestWrapper(DatastoreVendor.OpenSearch);

            // empty dictionary
            var addResponseGetter = wrapperBase.GetRequestResponseFromGeneric.GetOrAdd(responseTask.GetType(), (o) => ((Task<TestSearchRequest>)o).Result);
            var addResponse = addResponseGetter(responseTask);

            // non-empty dictionary
            var getResponseGetter = wrapperBase.GetRequestResponseFromGeneric.GetOrAdd(responseTask.GetType(), (o) => ((Task<TestSearchRequest>)o).Result);
            var getResponse = getResponseGetter(responseTask);

            Assert.That(addResponse, Is.Not.Null);
            Assert.That(addResponse, Is.EqualTo(request));

            Assert.That(getResponse, Is.Not.Null);
            Assert.That(getResponse, Is.EqualTo(request));
        }

        [Test]
        public void ValidTaskResponse()
        {
            var completedTask = Task.CompletedTask;
            var delayedTask = Task.Delay(3000);
            Assert.That(TestSearchRequestWrapper.ValidTaskResponse(completedTask), Is.True);
            Assert.That(TestSearchRequestWrapper.ValidTaskResponse(delayedTask), Is.False);
            Assert.That(TestSearchRequestWrapper.ValidTaskResponse(null), Is.False);
        }

        private object GetRequestParams(string paramsType)
        {
            switch (paramsType)
            {
                case "Search":
                    return new SearchRequestParameters();
                case "Scroll":
                    return new ScrollRequestParameters();
                default:
                    return null;
            }
        }
    }

    public class SearchRequestParameters
    {

    }

    public class ScrollRequestParameters
    {

    }

    public class TestSearchRequestWrapper : SearchRequestWrapperBase
    {
        public override DatastoreVendor Vendor { get;  }

        public new ConcurrentDictionary<Type, Func<object, object>> GetRequestResponseFromGeneric => base.GetRequestResponseFromGeneric;

        public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
        {
            return new CanWrapResponse("TestSearchRequestWrapper".Equals(methodInfo.RequestedWrapperName));
        }

        public TestSearchRequestWrapper(DatastoreVendor datastoreVendor)
        {
            Vendor = datastoreVendor;
        }

        public new ISegment BuildSegment(int requestParamsIndex, InstrumentedMethodCall instrumentedMethodCall, ITransaction transaction)
        {
            return base.BuildSegment(requestParamsIndex, instrumentedMethodCall, transaction);
        }

        public new void TryProcessResponse(IAgent agent, ITransaction transaction, object response, ISegment segment)
        {
            base.TryProcessResponse(agent, transaction, response, segment);
        }

        public new static bool ValidTaskResponse(Task response)
        {
            return SearchRequestWrapperBase.ValidTaskResponse(response);
        }
    }

    public class TestSearchRequest
    {
        public void TestRequest(string request, string path, object nothingToSeeHere, object requestParams)
        {

        }

        public async Task TestRequestAsync(string request, string path, object nothingToSeeHere, object ignoreMe, object requestParams)
        {
            await Task.Delay(0);
        }
    }

    public class TestSearchResponse
    {
        public object ApiCallDetails { get; }

        public object ApiCall { get; }

        public TestSearchResponse(bool successOrKnownError, string testHost, int testPort, bool hasException)
        {
            var apiInfo = new TestSearchResponseApiInfo(testHost, testPort, hasException, successOrKnownError);
            ApiCallDetails = apiInfo;
            ApiCall = apiInfo;
            
        }
    }

    public class TestSearchResponseApiInfo
    {
        public Uri Uri { get; }

        public Exception OriginalException { get; }

        public bool SuccessOrKnownError { get; }

        public TestSearchResponseApiInfo(string host, int port, bool hasException, bool successOrKnownError)
        {
            Uri = new Uri($"http://{host}:{port}");
            successOrKnownError = SuccessOrKnownError;
            if (hasException)
            {
                OriginalException = new Exception("OriginalException");
            }
        }
    }

    public class EvilTestSearchResponse
    {
        public object ApiCallDetails => throw new Exception("CompositeTests.EvilTestSearchResponseApiInfo");

        public object ApiCall => throw new Exception("CompositeTests.EvilTestSearchResponseApiInfo");
    }
}
