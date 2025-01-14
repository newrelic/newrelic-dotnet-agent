// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Threading.Tasks;
using NewRelic.Agent.Api;
using NewRelic.Agent.Core.Segments;
using NewRelic.Agent.Extensions.Helpers;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NUnit.Framework;

namespace CompositeTests
{
    [TestFixture]
    public class SearchRequestWrapperBaseTests
    {
        private static CompositeTestAgent _compositeTestAgent;

        private IAgent _agent;

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
        [TestCase("GET", "search", "scroll", null, false, DatastoreVendor.OpenSearch)]
        [TestCase("PUT", "_my-index", "_search", "Search", false, DatastoreVendor.OpenSearch)]
        [TestCase("GET", "_search", "scroll", "Scroll", false, DatastoreVendor.OpenSearch)]
        [TestCase("PUT", "_my-index", "_search", null, false, DatastoreVendor.OpenSearch)]
        [TestCase("GET", "_search", "scroll", null, false, DatastoreVendor.OpenSearch)]
        public void Test_BuildSegment_TryProcessResponse_Success(string request, string path, string operationData,
            string requestParams, bool isAsync, DatastoreVendor datastoreVendor)
        {
            var response = new TestSearchResponse(true);
            GetTestData(request, path, operationData, requestParams, isAsync, datastoreVendor, response,
                out var instrumentedMethodCall, out var segment, out var transaction);

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
            Assert.That(datastoreSegmentData.Host, Is.EqualTo("localhost"));
            Assert.That(datastoreSegmentData.Port, Is.EqualTo(1337));
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
            Assert.That(SearchRequestWrapperBase.ValidTaskResponse(completedTask), Is.True);
            Assert.That(SearchRequestWrapperBase.ValidTaskResponse(delayedTask), Is.False);
        }

        private void GetTestData(string request, string path, string operationData,
            string requestParams, bool isAsync, DatastoreVendor datastoreVendor, TestSearchResponse response,
            out InstrumentedMethodCall instrumentedMethodCall, out Segment segment, out ITransaction transaction)
        {
            var combinedPath = $"/{path}/{operationData}";

            var wrapperBase = new TestSearchRequestWrapper(datastoreVendor);
            var testRequest = new TestSearchRequest();
            var methodArgs = isAsync
                ? new object[] { request, combinedPath, null, null, GetRequestParams(requestParams) }
                : new object[] { request, combinedPath, null, GetRequestParams(requestParams) };

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

            instrumentedMethodCall = new InstrumentedMethodCall(methodCall, instrumentedMethodInfo);

            transaction = _agent.CreateTransaction(true, "mycategory", "mytransactionname", false);

            segment = wrapperBase.BuildSegment(isAsync, instrumentedMethodCall, transaction) as Segment;
            wrapperBase.TryProcessResponse(_agent, transaction, response, segment);
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
        public override int RequestParamsIndex => 3;
        public override int RequestParamsIndexAsync => 4;
        public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
        {
            return new CanWrapResponse("TestSearchRequestWrapper".Equals(methodInfo.RequestedWrapperName));
        }

        public TestSearchRequestWrapper(DatastoreVendor datastoreVendor)
        {
            Vendor = datastoreVendor;
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
        public object ApiCallDetails => new TestSearhResponseApiInfo();

        public object ApiCall => new TestSearhResponseApiInfo();

        public Exception OriginalException => new Exception("OriginalException");

        public bool SuccessOrKnownError { get; }

        public TestSearchResponse(bool successOrKnownError)
        {
            SuccessOrKnownError = successOrKnownError;
        }
    }

    public class TestSearhResponseApiInfo
    {
        public Uri Uri => new Uri("http://localhost:1337");
    }
}
