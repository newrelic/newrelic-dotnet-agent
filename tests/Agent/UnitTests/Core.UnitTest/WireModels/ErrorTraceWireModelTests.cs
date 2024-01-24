// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Attributes;
using NewRelic.Agent.Core.DataTransport;
using NewRelic.Agent.Core.Events;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Core.WireModels;
using NewRelic.Core;
using Newtonsoft.Json;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.Errors.UnitTest
{
    public class ErrorTraceWireModelTests
    {
        [TestFixture, Category("ErrorTraces")]
        public class Method_ToJsonObjectArray
        {
            private static readonly IDictionary<string, object> EmptyDictionary = new Dictionary<string, object>();

            private TestUtilities.Logging _logging;

            public const string AgentAttributesKey = "agentAttributes";
            public const string UserAttributesKey = "userAttributes";
            public const string IntrinsicsKey = "intrinsics";

            private IList<string> _stackTrace;
            private DateTime _timestamp;
            private string _path;
            private string _message;
            private string _exceptionClassName;
            private string _guid;
            private IDataTransportService _dataTransportService;
            private IAttributeDefinitionService _attribDefSvc;
            private IAttributeDefinitions _attribDefs => _attribDefSvc?.AttributeDefs;

            private static IList<string> CreateStackTrace()
            {
                var stackTrace = new List<string>
                {
                    "System.Exception: Inner Exception",
                    @"at WebApplication.Contact.Baz() in c:\code\dotnet_agent\Agent\NewRelic\Profiler\WebApplication1\Contact.aspx.cs:line 50",
                    @"at WebApplication.Contact.Foo() in c:\code\dotnet_agent\Agent\NewRelic\Profiler\WebApplication1\Contact.aspx.cs:line 40",
                    "--- End of inner exception stack trace ---",
                    @"at WebApplication.Contact.Foo() in c:\code\dotnet_agent\Agent\NewRelic\Profiler\WebApplication1\Contact.aspx.cs:line 46",
                    @"at WebApplication.Contact.Bar() in c:\code\dotnet_agent\Agent\NewRelic\Profiler\WebApplication1\Contact.aspx.cs:line 28",
                    "--- End of inner exception stack trace ---",
                    @"at WebApplication.Contact.Bar() in c:\code\dotnet_agent\Agent\NewRelic\Profiler\WebApplication1\Contact.aspx.cs:line 34",
                    @"at WebApplication.Contact.Page_Load(Object sender, EventArgs e) in c:\code\dotnet_agent\Agent\NewRelic\Profiler\WebApplication1\Contact.aspx.cs:line 22",
                    "at System.Web.UI.Control.LoadRecursive()",
                    "at System.Web.UI.Page.ProcessRequestMain(Boolean includeStagesBeforeAsyncPoint, Boolean includeStagesAfterAsyncPoint)",
                    "at System.Web.UI.Page.HandleError(Exception e)",
                    "at System.Web.UI.Page.ProcessRequestMain(Boolean includeStagesBeforeAsyncPoint, Boolean includeStagesAfterAsyncPoint)",
                    "at System.Web.UI.Page.ProcessRequest(Boolean includeStagesBeforeAsyncPoint, Boolean includeStagesAfterAsyncPoint)",
                    "at System.Web.UI.Page.ProcessRequest(Boolean includeStagesBeforeAsyncPoint, Boolean includeStagesAfterAsyncPoint)",
                    "at System.Web.UI.Page.ProcessRequest()",
                    "at System.Web.UI.Page.ProcessRequest(HttpContext context)",
                    @"at ASP.contact_aspx.ProcessRequest(HttpContext context) in c:\Windows\Microsoft.NET\Framework64\v4.0.30319\Temporary ASP.NET Files\webapplication1\734e4ee5\213b041b\App_Web_lag4whrl.2.cs:line 0",
                    "at System.Web.HttpApplication.CallHandlerExecutionStep.System.Web.HttpApplication.IExecutionStep.Execute()",
                    "at System.Web.HttpApplication.CallHandlerExecutionStep.System.Web.HttpApplication.IExecutionStep.Execute()",
                    "at System.Web.HttpApplication.ExecuteStep(IExecutionStep step, Boolean& completedSynchronously)}"
                };
                return stackTrace;

            }

            private static IConfiguration CreateMockConfiguration()
            {
                var configuration = Mock.Create<IConfiguration>();
                Mock.Arrange(() => configuration.CaptureCustomParameters).Returns(true);
                Mock.Arrange(() => configuration.CaptureAttributes).Returns(true);
                Mock.Arrange(() => configuration.CaptureAttributesExcludes)
                    .Returns(new List<string>() { "identity.*", "request.headers.*", "response.headers.*" });
                Mock.Arrange(() => configuration.CaptureAttributesIncludes).Returns(new string[] { "request.parameters.*" });

                return configuration;
            }

            [SetUp]
            public void SetUp()
            {
                _dataTransportService = Mock.Create<IDataTransportService>();
                _logging = new TestUtilities.Logging();

                EventBus<ConfigurationUpdatedEvent>.Publish(new ConfigurationUpdatedEvent(CreateMockConfiguration(),
                    ConfigurationUpdateSource.Unknown));

                _timestamp = new DateTime(2018, 1, 1, 1, 0, 0);
                _path = "WebTransaction/ASP/post.aspx";
                _message = "The Error Message";
                _exceptionClassName = "System.MyErrorClassName";
                _stackTrace = CreateStackTrace();
                _guid = "123";

                _attribDefSvc = new AttributeDefinitionService((f) => new AttributeDefinitions(f));

            }

            [TearDown]
            public void TearDown()
            {
                _attribDefSvc.Dispose();
                _logging.Dispose();
            }

            [Test]
            public void when_default_fixture_values_are_used_then_serializes_correctly()
            {
                var attribValues = new AttributeValueCollection(AttributeDestinations.ErrorTrace);

                var attributes = new ErrorTraceWireModel.ErrorTraceAttributesWireModel(attribValues, _stackTrace);
                var errorTraceData = new ErrorTraceWireModel(_timestamp, _path, _message, _exceptionClassName, attributes, _guid);

                var expectedResult =
                    "["
                    + _timestamp.ToUnixTimeMilliseconds()
                    + ",\"" + _path + "\",\""
                    + _message + "\",\""
                    + _exceptionClassName + "\",{"
                    + "\"stack_trace\":"
                    +
                    "[\"System.Exception: Inner Exception\",\"at WebApplication.Contact.Baz() in c:\\\\code\\\\dotnet_agent\\\\Agent\\\\NewRelic\\\\Profiler\\\\WebApplication1\\\\Contact.aspx.cs:line 50\",\"at WebApplication.Contact.Foo() in c:\\\\code\\\\dotnet_agent\\\\Agent\\\\NewRelic\\\\Profiler\\\\WebApplication1\\\\Contact.aspx.cs:line 40\",\"--- End of inner exception stack trace ---\",\"at WebApplication.Contact.Foo() in c:\\\\code\\\\dotnet_agent\\\\Agent\\\\NewRelic\\\\Profiler\\\\WebApplication1\\\\Contact.aspx.cs:line 46\",\"at WebApplication.Contact.Bar() in c:\\\\code\\\\dotnet_agent\\\\Agent\\\\NewRelic\\\\Profiler\\\\WebApplication1\\\\Contact.aspx.cs:line 28\",\"--- End of inner exception stack trace ---\",\"at WebApplication.Contact.Bar() in c:\\\\code\\\\dotnet_agent\\\\Agent\\\\NewRelic\\\\Profiler\\\\WebApplication1\\\\Contact.aspx.cs:line 34\",\"at WebApplication.Contact.Page_Load(Object sender, EventArgs e) in c:\\\\code\\\\dotnet_agent\\\\Agent\\\\NewRelic\\\\Profiler\\\\WebApplication1\\\\Contact.aspx.cs:line 22\",\"at System.Web.UI.Control.LoadRecursive()\",\"at System.Web.UI.Page.ProcessRequestMain(Boolean includeStagesBeforeAsyncPoint, Boolean includeStagesAfterAsyncPoint)\",\"at System.Web.UI.Page.HandleError(Exception e)\",\"at System.Web.UI.Page.ProcessRequestMain(Boolean includeStagesBeforeAsyncPoint, Boolean includeStagesAfterAsyncPoint)\",\"at System.Web.UI.Page.ProcessRequest(Boolean includeStagesBeforeAsyncPoint, Boolean includeStagesAfterAsyncPoint)\",\"at System.Web.UI.Page.ProcessRequest(Boolean includeStagesBeforeAsyncPoint, Boolean includeStagesAfterAsyncPoint)\",\"at System.Web.UI.Page.ProcessRequest()\",\"at System.Web.UI.Page.ProcessRequest(HttpContext context)\",\"at ASP.contact_aspx.ProcessRequest(HttpContext context) in c:\\\\Windows\\\\Microsoft.NET\\\\Framework64\\\\v4.0.30319\\\\Temporary ASP.NET Files\\\\webapplication1\\\\734e4ee5\\\\213b041b\\\\App_Web_lag4whrl.2.cs:line 0\",\"at System.Web.HttpApplication.CallHandlerExecutionStep.System.Web.HttpApplication.IExecutionStep.Execute()\",\"at System.Web.HttpApplication.CallHandlerExecutionStep.System.Web.HttpApplication.IExecutionStep.Execute()\",\"at System.Web.HttpApplication.ExecuteStep(IExecutionStep step, Boolean& completedSynchronously)}\"],"
                    + "\"" + AgentAttributesKey + "\":{},"
                    + "\"" + UserAttributesKey + "\":{},"
                    + "\"" + IntrinsicsKey + "\":{}},"
                    + "\"123\"]";

                var actualResult = JsonConvert.SerializeObject(errorTraceData);
                Assert.That(actualResult, Is.EqualTo(expectedResult));
            }

            [Test]
            public void eror_trace_attributes_when_default_fixture_values_are_used_then_serializes_correctly()
            {
                var attribValues = new AttributeValueCollection(AttributeDestinations.ErrorTrace);

                var attributes = new ErrorTraceWireModel.ErrorTraceAttributesWireModel(attribValues, _stackTrace);

                var expectedResult =
                    "{\"stack_trace\":"
                    +
                    "[\"System.Exception: Inner Exception\",\"at WebApplication.Contact.Baz() in c:\\\\code\\\\dotnet_agent\\\\Agent\\\\NewRelic\\\\Profiler\\\\WebApplication1\\\\Contact.aspx.cs:line 50\",\"at WebApplication.Contact.Foo() in c:\\\\code\\\\dotnet_agent\\\\Agent\\\\NewRelic\\\\Profiler\\\\WebApplication1\\\\Contact.aspx.cs:line 40\",\"--- End of inner exception stack trace ---\",\"at WebApplication.Contact.Foo() in c:\\\\code\\\\dotnet_agent\\\\Agent\\\\NewRelic\\\\Profiler\\\\WebApplication1\\\\Contact.aspx.cs:line 46\",\"at WebApplication.Contact.Bar() in c:\\\\code\\\\dotnet_agent\\\\Agent\\\\NewRelic\\\\Profiler\\\\WebApplication1\\\\Contact.aspx.cs:line 28\",\"--- End of inner exception stack trace ---\",\"at WebApplication.Contact.Bar() in c:\\\\code\\\\dotnet_agent\\\\Agent\\\\NewRelic\\\\Profiler\\\\WebApplication1\\\\Contact.aspx.cs:line 34\",\"at WebApplication.Contact.Page_Load(Object sender, EventArgs e) in c:\\\\code\\\\dotnet_agent\\\\Agent\\\\NewRelic\\\\Profiler\\\\WebApplication1\\\\Contact.aspx.cs:line 22\",\"at System.Web.UI.Control.LoadRecursive()\",\"at System.Web.UI.Page.ProcessRequestMain(Boolean includeStagesBeforeAsyncPoint, Boolean includeStagesAfterAsyncPoint)\",\"at System.Web.UI.Page.HandleError(Exception e)\",\"at System.Web.UI.Page.ProcessRequestMain(Boolean includeStagesBeforeAsyncPoint, Boolean includeStagesAfterAsyncPoint)\",\"at System.Web.UI.Page.ProcessRequest(Boolean includeStagesBeforeAsyncPoint, Boolean includeStagesAfterAsyncPoint)\",\"at System.Web.UI.Page.ProcessRequest(Boolean includeStagesBeforeAsyncPoint, Boolean includeStagesAfterAsyncPoint)\",\"at System.Web.UI.Page.ProcessRequest()\",\"at System.Web.UI.Page.ProcessRequest(HttpContext context)\",\"at ASP.contact_aspx.ProcessRequest(HttpContext context) in c:\\\\Windows\\\\Microsoft.NET\\\\Framework64\\\\v4.0.30319\\\\Temporary ASP.NET Files\\\\webapplication1\\\\734e4ee5\\\\213b041b\\\\App_Web_lag4whrl.2.cs:line 0\",\"at System.Web.HttpApplication.CallHandlerExecutionStep.System.Web.HttpApplication.IExecutionStep.Execute()\",\"at System.Web.HttpApplication.CallHandlerExecutionStep.System.Web.HttpApplication.IExecutionStep.Execute()\",\"at System.Web.HttpApplication.ExecuteStep(IExecutionStep step, Boolean& completedSynchronously)}\"],"
                    + "\"" + AgentAttributesKey + "\":{},"
                    + "\"" + UserAttributesKey + "\":{},"
                    + "\"" + IntrinsicsKey + "\":{}}";

                var actualResult = JsonConvert.SerializeObject(attributes);
                Assert.That(actualResult, Is.EqualTo(expectedResult));
            }

            [Test]
            public void eror_trace_attributes_when_default_fixture_values_are_used_then_serializes_correctly_with_legacy_serializer()
            {
                var attribValues = new AttributeValueCollection(AttributeDestinations.ErrorTrace);

                var attributes = new ErrorTraceWireModel.ErrorTraceAttributesWireModel(attribValues, _stackTrace);

                var expectedResult =
                    "{\"stack_trace\":"
                    +
                    "[\"System.Exception: Inner Exception\",\"at WebApplication.Contact.Baz() in c:\\\\code\\\\dotnet_agent\\\\Agent\\\\NewRelic\\\\Profiler\\\\WebApplication1\\\\Contact.aspx.cs:line 50\",\"at WebApplication.Contact.Foo() in c:\\\\code\\\\dotnet_agent\\\\Agent\\\\NewRelic\\\\Profiler\\\\WebApplication1\\\\Contact.aspx.cs:line 40\",\"--- End of inner exception stack trace ---\",\"at WebApplication.Contact.Foo() in c:\\\\code\\\\dotnet_agent\\\\Agent\\\\NewRelic\\\\Profiler\\\\WebApplication1\\\\Contact.aspx.cs:line 46\",\"at WebApplication.Contact.Bar() in c:\\\\code\\\\dotnet_agent\\\\Agent\\\\NewRelic\\\\Profiler\\\\WebApplication1\\\\Contact.aspx.cs:line 28\",\"--- End of inner exception stack trace ---\",\"at WebApplication.Contact.Bar() in c:\\\\code\\\\dotnet_agent\\\\Agent\\\\NewRelic\\\\Profiler\\\\WebApplication1\\\\Contact.aspx.cs:line 34\",\"at WebApplication.Contact.Page_Load(Object sender, EventArgs e) in c:\\\\code\\\\dotnet_agent\\\\Agent\\\\NewRelic\\\\Profiler\\\\WebApplication1\\\\Contact.aspx.cs:line 22\",\"at System.Web.UI.Control.LoadRecursive()\",\"at System.Web.UI.Page.ProcessRequestMain(Boolean includeStagesBeforeAsyncPoint, Boolean includeStagesAfterAsyncPoint)\",\"at System.Web.UI.Page.HandleError(Exception e)\",\"at System.Web.UI.Page.ProcessRequestMain(Boolean includeStagesBeforeAsyncPoint, Boolean includeStagesAfterAsyncPoint)\",\"at System.Web.UI.Page.ProcessRequest(Boolean includeStagesBeforeAsyncPoint, Boolean includeStagesAfterAsyncPoint)\",\"at System.Web.UI.Page.ProcessRequest(Boolean includeStagesBeforeAsyncPoint, Boolean includeStagesAfterAsyncPoint)\",\"at System.Web.UI.Page.ProcessRequest()\",\"at System.Web.UI.Page.ProcessRequest(HttpContext context)\",\"at ASP.contact_aspx.ProcessRequest(HttpContext context) in c:\\\\Windows\\\\Microsoft.NET\\\\Framework64\\\\v4.0.30319\\\\Temporary ASP.NET Files\\\\webapplication1\\\\734e4ee5\\\\213b041b\\\\App_Web_lag4whrl.2.cs:line 0\",\"at System.Web.HttpApplication.CallHandlerExecutionStep.System.Web.HttpApplication.IExecutionStep.Execute()\",\"at System.Web.HttpApplication.CallHandlerExecutionStep.System.Web.HttpApplication.IExecutionStep.Execute()\",\"at System.Web.HttpApplication.ExecuteStep(IExecutionStep step, Boolean& completedSynchronously)}\"],"
                    + "\"" + AgentAttributesKey + "\":{},"
                    + "\"" + UserAttributesKey + "\":{},"
                    + "\"" + IntrinsicsKey + "\":{}}";

                var actualResult = JsonConvert.SerializeObject(attributes);
                Assert.That(actualResult, Is.EqualTo(expectedResult));
            }

            [Test]
            public void when_construtor_used_timestamp_property_is_set()
            {
                var attribValues = new AttributeValueCollection(AttributeDestinations.ErrorTrace);

                var attributes = new ErrorTraceWireModel.ErrorTraceAttributesWireModel(attribValues, _stackTrace);
                var errorTraceData = new ErrorTraceWireModel(_timestamp, _path, _message, _exceptionClassName, attributes, _guid);

                Assert.That(errorTraceData.TimeStamp, Is.EqualTo(_timestamp));
            }

            [Test]
            public void when_construtor_used_path_property_is_set()
            {
                var attribValues = new AttributeValueCollection(AttributeDestinations.ErrorTrace);

                var attributes = new ErrorTraceWireModel.ErrorTraceAttributesWireModel(attribValues, _stackTrace);
                var errorTraceData = new ErrorTraceWireModel(_timestamp, _path, _message, _exceptionClassName, attributes, _guid);

                Assert.That(errorTraceData.Path, Is.EqualTo(_path));
            }

            [Test]
            public void when_construtor_used_message_property_is_set()
            {
                var attribValues = new AttributeValueCollection(AttributeDestinations.ErrorTrace);

                var attributes = new ErrorTraceWireModel.ErrorTraceAttributesWireModel(attribValues, _stackTrace);
                var errorTraceData = new ErrorTraceWireModel(_timestamp, _path, _message, _exceptionClassName, attributes, _guid);

                Assert.That(errorTraceData.Message, Is.EqualTo(_message));
            }

            [Test]
            public void when_construtor_used_exceptionClassName_property_is_set()
            {
                var attribValues = new AttributeValueCollection(AttributeDestinations.ErrorTrace);

                var attributes = new ErrorTraceWireModel.ErrorTraceAttributesWireModel(attribValues, _stackTrace);
                var errorTraceData = new ErrorTraceWireModel(_timestamp, _path, _message, _exceptionClassName, attributes, _guid);

                Assert.That(errorTraceData.ExceptionClassName, Is.EqualTo(_exceptionClassName));
            }

            [Test]
            public void when_construtor_used_parameters_property_is_set()
            {
                var attribValues = new AttributeValueCollection(AttributeDestinations.ErrorTrace);

                var attributes = new ErrorTraceWireModel.ErrorTraceAttributesWireModel(attribValues, _stackTrace);
                var errorTraceData = new ErrorTraceWireModel(_timestamp, _path, _message, _exceptionClassName, attributes, _guid);

                Assert.That(errorTraceData.Attributes, Is.EqualTo(attributes));
            }

            [Test]
            public void when_construtor_used_guid_property_is_set()
            {
                var attribValues = new AttributeValueCollection(AttributeDestinations.ErrorTrace);

                var attributes = new ErrorTraceWireModel.ErrorTraceAttributesWireModel(attribValues,_stackTrace);
                var errorTraceData = new ErrorTraceWireModel(_timestamp, _path, _message, _exceptionClassName, attributes, _guid);

                Assert.That(errorTraceData.Guid, Is.EqualTo(_guid));
            }

            [Test]
            public void when_agentAttributes_are_supplied_then_they_show_up_in_json()
            {
                // ARRANGE
                var attribValues = new AttributeValueCollection(AttributeDestinations.ErrorTrace);
                _attribDefs.OriginalUrl.TrySetValue(attribValues, "www.test.com");

                // ACT
                var attributes = new ErrorTraceWireModel.ErrorTraceAttributesWireModel(attribValues, _stackTrace);
                var errorTraceData = new ErrorTraceWireModel(_timestamp, _path, _message, _exceptionClassName, attributes, _guid);
                var errorTraceDataJson = JsonConvert.SerializeObject(errorTraceData);

                // ASSERT
                Assert.That(errorTraceDataJson, Does.Contain(@"""agentAttributes"":{""original_url"":""www.test.com""}"));
            }

            [Test]
            public void when_intrinsicAttributes_are_supplied_then_they_show_up_in_json()
            {
                // ARRANGE
                var attribValues = new AttributeValueCollection(AttributeDestinations.ErrorTrace);
                _attribDefs.Guid.TrySetValue(attribValues, "GuidTestValue");

                // ACT
                var attributes = new ErrorTraceWireModel.ErrorTraceAttributesWireModel(attribValues, _stackTrace);
                var errorTraceData = new ErrorTraceWireModel(_timestamp, _path, _message, _exceptionClassName, attributes, _guid);
                var errorTraceDataJson = JsonConvert.SerializeObject(errorTraceData);

                // ASSERT
                Assert.That(errorTraceDataJson, Does.Contain(@"""intrinsics"":{""guid"":""GuidTestValue""}"));
            }

            [Test]
            public void when_userAttributes_are_supplied_then_they_show_up_in_json()
            {
                // ARRANGE
                var attribValues = new AttributeValueCollection(AttributeDestinations.ErrorTrace);
                _attribDefs.GetCustomAttributeForError("Foo").TrySetValue(attribValues, "Bar");

                // ACT
                var attributes = new ErrorTraceWireModel.ErrorTraceAttributesWireModel(attribValues, _stackTrace);
                var errorTraceData = new ErrorTraceWireModel(_timestamp, _path, _message, _exceptionClassName, attributes, _guid);
                var errorTraceDataJson = JsonConvert.SerializeObject(errorTraceData);

                // ASSERT
                Assert.That(errorTraceDataJson, Does.Contain(@"""userAttributes"":{""Foo"":""Bar""}"));
            }

            [Test]
            public void when_attributes_are_empty_then_it_does_not_show_up()
            {
                var attribValues = new AttributeValueCollection(AttributeDestinations.ErrorTrace);

                // ACT
                var attributes = new ErrorTraceWireModel.ErrorTraceAttributesWireModel(attribValues, _stackTrace);
                var errorTraceData = new ErrorTraceWireModel(_timestamp, _path, _message, _exceptionClassName, attributes, _guid);
                var errorTraceDataJson = JsonConvert.SerializeObject(errorTraceData);

                // ASSERT
                Assert.That(errorTraceDataJson, Does.Contain(@"""agentAttributes"":{}"));
                Assert.That(errorTraceDataJson, Does.Contain(@"""intrinsics"":{}"));
                Assert.That(errorTraceDataJson, Does.Contain(@"""userAttributes"":{}"));
            }

            [Test]
            public void when_stack_trace_is_null_then_exception_is_not_thrown()
            {
                // ACT
                Assert.DoesNotThrow(
                    () =>
                        new ErrorTraceWireModel.ErrorTraceAttributesWireModel(new AttributeValueCollection(AttributeDestinations.ErrorTrace), null));
            }
        }
    }
}
