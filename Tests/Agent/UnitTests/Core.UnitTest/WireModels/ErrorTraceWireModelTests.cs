using JetBrains.Annotations;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.DataTransport;
using NewRelic.Agent.Core.Events;
using NewRelic.Agent.Core.Transactions;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Core.WireModels;
using NewRelic.Core;
using Newtonsoft.Json;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using Telerik.JustMock;

// ReSharper disable InconsistentNaming
// ReSharper disable CheckNamespace
// ReSharper disable ClassNeverInstantiated.Global

namespace NewRelic.Agent.Core.Errors.UnitTest
{
	public class ErrorTraceWireModelTests
	{
		[TestFixture, Category("ErrorTraces")]
		public class Method_ToJsonObjectArray
		{
			[NotNull] private static readonly IDictionary<String, Object> EmptyDictionary = new Dictionary<String, Object>();

			[NotNull] private DisposableCollection _disposables;
			[NotNull] private TestUtilities.Logging _logging;
			[NotNull] private Attributes _attributes;

			[NotNull] private IList<String> _stackTrace;
			private DateTime _timestamp;
			[NotNull] private String _path;
			[NotNull] private String _message;
			[NotNull] private String _exceptionClassName;
			[NotNull] private String _guid;
			[NotNull] private IDataTransportService _dataTransportService;

			[NotNull]
			private static IList<String> CreateStackTrace()
			{
				var stackTrace = new List<String>
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

			[NotNull]
			private static IConfiguration CreateMockConfiguration()
			{
				var configuration = Mock.Create<IConfiguration>();
				Mock.Arrange(() => configuration.CaptureCustomParameters).Returns(true);
				Mock.Arrange(() => configuration.CaptureAttributes).Returns(true);
				Mock.Arrange(() => configuration.CaptureAttributesExcludes)
					.Returns(new List<String>() {"identity.*", "request.headers.*", "response.headers.*"});
				//Mock.Arrange(() => configuration.CaptureIdentityParameters).Returns(true);
				Mock.Arrange(() => configuration.CaptureRequestParameters).Returns(true);
				//Mock.Arrange(() => configuration.CaptureRequestHeaders).Returns(true);
				//Mock.Arrange(() => configuration.CaptureResponseHeaderParameters).Returns(true);
				return configuration;
			}

			[SetUp]
			public void SetUp()
			{
				_dataTransportService = Mock.Create<IDataTransportService>();
				_attributes = new Attributes();
				_logging = new TestUtilities.Logging();
				_disposables = new DisposableCollection
				{
					_logging
				};

				EventBus<ConfigurationUpdatedEvent>.Publish(new ConfigurationUpdatedEvent(CreateMockConfiguration(),
					ConfigurationUpdateSource.Unknown));

				_timestamp = new DateTime(2018, 1, 1, 1, 0, 0);
				_path = "WebTransaction/ASP/post.aspx";
				_message = "The Error Message";
				_exceptionClassName = "System.MyErrorClassName";
				_stackTrace = CreateStackTrace();
				_guid = "123";
			}

			[TearDown]
			public void TearDown()
			{
				_disposables.Dispose();
			}

			[Test]
			public void when_default_fixture_values_are_used_then_serializes_correctly()
			{
				var attributes = new ErrorTraceWireModel.ErrorTraceAttributesWireModel(EmptyDictionary, EmptyDictionary,
					EmptyDictionary, _stackTrace);
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
					+ "\"" + AttributeService.AgentAttributesKey + "\":{},"
					+ "\"" + AttributeService.UserAttributesKey + "\":{},"
					+ "\"" + AttributeService.IntrinsicsKey + "\":{}},"
					+ "\"123\"]";

				var actualResult = JsonConvert.SerializeObject(errorTraceData);
				Assert.AreEqual(expectedResult, actualResult);
			}

			[Test]
			public void eror_trace_attributes_when_default_fixture_values_are_used_then_serializes_correctly()
			{
				var attributes = new ErrorTraceWireModel.ErrorTraceAttributesWireModel(EmptyDictionary, EmptyDictionary,
					EmptyDictionary, _stackTrace);

				var expectedResult =
					"{\"stack_trace\":"
					+
					"[\"System.Exception: Inner Exception\",\"at WebApplication.Contact.Baz() in c:\\\\code\\\\dotnet_agent\\\\Agent\\\\NewRelic\\\\Profiler\\\\WebApplication1\\\\Contact.aspx.cs:line 50\",\"at WebApplication.Contact.Foo() in c:\\\\code\\\\dotnet_agent\\\\Agent\\\\NewRelic\\\\Profiler\\\\WebApplication1\\\\Contact.aspx.cs:line 40\",\"--- End of inner exception stack trace ---\",\"at WebApplication.Contact.Foo() in c:\\\\code\\\\dotnet_agent\\\\Agent\\\\NewRelic\\\\Profiler\\\\WebApplication1\\\\Contact.aspx.cs:line 46\",\"at WebApplication.Contact.Bar() in c:\\\\code\\\\dotnet_agent\\\\Agent\\\\NewRelic\\\\Profiler\\\\WebApplication1\\\\Contact.aspx.cs:line 28\",\"--- End of inner exception stack trace ---\",\"at WebApplication.Contact.Bar() in c:\\\\code\\\\dotnet_agent\\\\Agent\\\\NewRelic\\\\Profiler\\\\WebApplication1\\\\Contact.aspx.cs:line 34\",\"at WebApplication.Contact.Page_Load(Object sender, EventArgs e) in c:\\\\code\\\\dotnet_agent\\\\Agent\\\\NewRelic\\\\Profiler\\\\WebApplication1\\\\Contact.aspx.cs:line 22\",\"at System.Web.UI.Control.LoadRecursive()\",\"at System.Web.UI.Page.ProcessRequestMain(Boolean includeStagesBeforeAsyncPoint, Boolean includeStagesAfterAsyncPoint)\",\"at System.Web.UI.Page.HandleError(Exception e)\",\"at System.Web.UI.Page.ProcessRequestMain(Boolean includeStagesBeforeAsyncPoint, Boolean includeStagesAfterAsyncPoint)\",\"at System.Web.UI.Page.ProcessRequest(Boolean includeStagesBeforeAsyncPoint, Boolean includeStagesAfterAsyncPoint)\",\"at System.Web.UI.Page.ProcessRequest(Boolean includeStagesBeforeAsyncPoint, Boolean includeStagesAfterAsyncPoint)\",\"at System.Web.UI.Page.ProcessRequest()\",\"at System.Web.UI.Page.ProcessRequest(HttpContext context)\",\"at ASP.contact_aspx.ProcessRequest(HttpContext context) in c:\\\\Windows\\\\Microsoft.NET\\\\Framework64\\\\v4.0.30319\\\\Temporary ASP.NET Files\\\\webapplication1\\\\734e4ee5\\\\213b041b\\\\App_Web_lag4whrl.2.cs:line 0\",\"at System.Web.HttpApplication.CallHandlerExecutionStep.System.Web.HttpApplication.IExecutionStep.Execute()\",\"at System.Web.HttpApplication.CallHandlerExecutionStep.System.Web.HttpApplication.IExecutionStep.Execute()\",\"at System.Web.HttpApplication.ExecuteStep(IExecutionStep step, Boolean& completedSynchronously)}\"],"
					+ "\"" + AttributeService.AgentAttributesKey + "\":{},"
					+ "\"" + AttributeService.UserAttributesKey + "\":{},"
					+ "\"" + AttributeService.IntrinsicsKey + "\":{}}";

				var actualResult = JsonConvert.SerializeObject(attributes);
				Assert.AreEqual(expectedResult, actualResult);
			}

			[Test]
			public void
				eror_trace_attributes_when_default_fixture_values_are_used_then_serializes_correctly_with_legacy_serializer()
			{
				var attributes = new ErrorTraceWireModel.ErrorTraceAttributesWireModel(EmptyDictionary, EmptyDictionary,
					EmptyDictionary, _stackTrace);

				var expectedResult =
					"{\"stack_trace\":"
					+
					"[\"System.Exception: Inner Exception\",\"at WebApplication.Contact.Baz() in c:\\\\code\\\\dotnet_agent\\\\Agent\\\\NewRelic\\\\Profiler\\\\WebApplication1\\\\Contact.aspx.cs:line 50\",\"at WebApplication.Contact.Foo() in c:\\\\code\\\\dotnet_agent\\\\Agent\\\\NewRelic\\\\Profiler\\\\WebApplication1\\\\Contact.aspx.cs:line 40\",\"--- End of inner exception stack trace ---\",\"at WebApplication.Contact.Foo() in c:\\\\code\\\\dotnet_agent\\\\Agent\\\\NewRelic\\\\Profiler\\\\WebApplication1\\\\Contact.aspx.cs:line 46\",\"at WebApplication.Contact.Bar() in c:\\\\code\\\\dotnet_agent\\\\Agent\\\\NewRelic\\\\Profiler\\\\WebApplication1\\\\Contact.aspx.cs:line 28\",\"--- End of inner exception stack trace ---\",\"at WebApplication.Contact.Bar() in c:\\\\code\\\\dotnet_agent\\\\Agent\\\\NewRelic\\\\Profiler\\\\WebApplication1\\\\Contact.aspx.cs:line 34\",\"at WebApplication.Contact.Page_Load(Object sender, EventArgs e) in c:\\\\code\\\\dotnet_agent\\\\Agent\\\\NewRelic\\\\Profiler\\\\WebApplication1\\\\Contact.aspx.cs:line 22\",\"at System.Web.UI.Control.LoadRecursive()\",\"at System.Web.UI.Page.ProcessRequestMain(Boolean includeStagesBeforeAsyncPoint, Boolean includeStagesAfterAsyncPoint)\",\"at System.Web.UI.Page.HandleError(Exception e)\",\"at System.Web.UI.Page.ProcessRequestMain(Boolean includeStagesBeforeAsyncPoint, Boolean includeStagesAfterAsyncPoint)\",\"at System.Web.UI.Page.ProcessRequest(Boolean includeStagesBeforeAsyncPoint, Boolean includeStagesAfterAsyncPoint)\",\"at System.Web.UI.Page.ProcessRequest(Boolean includeStagesBeforeAsyncPoint, Boolean includeStagesAfterAsyncPoint)\",\"at System.Web.UI.Page.ProcessRequest()\",\"at System.Web.UI.Page.ProcessRequest(HttpContext context)\",\"at ASP.contact_aspx.ProcessRequest(HttpContext context) in c:\\\\Windows\\\\Microsoft.NET\\\\Framework64\\\\v4.0.30319\\\\Temporary ASP.NET Files\\\\webapplication1\\\\734e4ee5\\\\213b041b\\\\App_Web_lag4whrl.2.cs:line 0\",\"at System.Web.HttpApplication.CallHandlerExecutionStep.System.Web.HttpApplication.IExecutionStep.Execute()\",\"at System.Web.HttpApplication.CallHandlerExecutionStep.System.Web.HttpApplication.IExecutionStep.Execute()\",\"at System.Web.HttpApplication.ExecuteStep(IExecutionStep step, Boolean& completedSynchronously)}\"],"
					+ "\"" + AttributeService.AgentAttributesKey + "\":{},"
					+ "\"" + AttributeService.UserAttributesKey + "\":{},"
					+ "\"" + AttributeService.IntrinsicsKey + "\":{}}";

				var actualResult = JsonConvert.SerializeObject(attributes);
				Assert.AreEqual(expectedResult, actualResult);
			}

			[Test]
			public void when_construtor_used_timestamp_property_is_set()
			{
				var attributes = new ErrorTraceWireModel.ErrorTraceAttributesWireModel(EmptyDictionary, EmptyDictionary,
					EmptyDictionary, _stackTrace);
				var errorTraceData = new ErrorTraceWireModel(_timestamp, _path, _message, _exceptionClassName, attributes, _guid);

				Assert.AreEqual(_timestamp, errorTraceData.TimeStamp);
			}

			[Test]
			public void when_construtor_used_path_property_is_set()
			{
				var attributes = new ErrorTraceWireModel.ErrorTraceAttributesWireModel(EmptyDictionary, EmptyDictionary,
					EmptyDictionary, _stackTrace);
				var errorTraceData = new ErrorTraceWireModel(_timestamp, _path, _message, _exceptionClassName, attributes, _guid);

				Assert.AreEqual(_path, errorTraceData.Path);
			}

			[Test]
			public void when_construtor_used_message_property_is_set()
			{
				var attributes = new ErrorTraceWireModel.ErrorTraceAttributesWireModel(EmptyDictionary, EmptyDictionary,
					EmptyDictionary, _stackTrace);
				var errorTraceData = new ErrorTraceWireModel(_timestamp, _path, _message, _exceptionClassName, attributes, _guid);

				Assert.AreEqual(_message, errorTraceData.Message);
			}

			[Test]
			public void when_construtor_used_exceptionClassName_property_is_set()
			{
				var attributes = new ErrorTraceWireModel.ErrorTraceAttributesWireModel(EmptyDictionary, EmptyDictionary,
					EmptyDictionary, _stackTrace);
				var errorTraceData = new ErrorTraceWireModel(_timestamp, _path, _message, _exceptionClassName, attributes, _guid);

				Assert.AreEqual(_exceptionClassName, errorTraceData.ExceptionClassName);
			}

			[Test]
			public void when_construtor_used_parameters_property_is_set()
			{
				var attributes = new ErrorTraceWireModel.ErrorTraceAttributesWireModel(EmptyDictionary, EmptyDictionary,
					EmptyDictionary, _stackTrace);
				var errorTraceData = new ErrorTraceWireModel(_timestamp, _path, _message, _exceptionClassName, attributes, _guid);

				Assert.AreEqual(attributes, errorTraceData.Attributes);
			}

			[Test]
			public void when_construtor_used_guid_property_is_set()
			{
				var attributes = new ErrorTraceWireModel.ErrorTraceAttributesWireModel(EmptyDictionary, EmptyDictionary,
					EmptyDictionary, _stackTrace);
				var errorTraceData = new ErrorTraceWireModel(_timestamp, _path, _message, _exceptionClassName, attributes, _guid);

				Assert.AreEqual(_guid, errorTraceData.Guid);
			}

			[Test]
			public void when_agentAttributes_are_supplied_then_they_show_up_in_json()
			{
				// ARRANGE
				var agentAttributes = new Dictionary<String, Object> {{"Foo", "Bar"}};

				// ACT
				var attributes = new ErrorTraceWireModel.ErrorTraceAttributesWireModel(agentAttributes, EmptyDictionary,
					EmptyDictionary, _stackTrace);
				var errorTraceData = new ErrorTraceWireModel(_timestamp, _path, _message, _exceptionClassName, attributes, _guid);
				var errorTraceDataJson = JsonConvert.SerializeObject(errorTraceData);

				// ASSERT
				StringAssert.Contains(@"""agentAttributes"":{""Foo"":""Bar""}", errorTraceDataJson);
			}

			[Test]
			public void when_intrinsicAttributes_are_supplied_then_they_show_up_in_json()
			{
				// ARRANGE
				var agentAttributes = new Dictionary<String, Object> {{"Foo", "Bar"}};

				// ACT
				var attributes = new ErrorTraceWireModel.ErrorTraceAttributesWireModel(EmptyDictionary, agentAttributes,
					EmptyDictionary, _stackTrace);
				var errorTraceData = new ErrorTraceWireModel(_timestamp, _path, _message, _exceptionClassName, attributes, _guid);
				var errorTraceDataJson = JsonConvert.SerializeObject(errorTraceData);

				// ASSERT
				StringAssert.Contains(@"""intrinsics"":{""Foo"":""Bar""}", errorTraceDataJson);
			}

			[Test]
			public void when_userAttributes_are_supplied_then_they_show_up_in_json()
			{
				// ARRANGE
				var userAttributes = new Dictionary<String, Object> {{"Foo", "Bar"}};

				// ACT
				var attributes = new ErrorTraceWireModel.ErrorTraceAttributesWireModel(EmptyDictionary, EmptyDictionary,
					userAttributes, _stackTrace);
				var errorTraceData = new ErrorTraceWireModel(_timestamp, _path, _message, _exceptionClassName, attributes, _guid);
				var errorTraceDataJson = JsonConvert.SerializeObject(errorTraceData);

				// ASSERT
				StringAssert.Contains(@"""userAttributes"":{""Foo"":""Bar""}", errorTraceDataJson);
			}

			[Test]
			public void when_attributes_are_empty_then_it_does_not_show_up()
			{
				// ACT
				var attributes = new ErrorTraceWireModel.ErrorTraceAttributesWireModel(EmptyDictionary, EmptyDictionary,
					EmptyDictionary, _stackTrace);
				var errorTraceData = new ErrorTraceWireModel(_timestamp, _path, _message, _exceptionClassName, attributes, _guid);
				var errorTraceDataJson = JsonConvert.SerializeObject(errorTraceData);

				// ASSERT
				StringAssert.Contains(@"""agentAttributes"":{}", errorTraceDataJson);
				StringAssert.Contains(@"""intrinsics"":{}", errorTraceDataJson);
				StringAssert.Contains(@"""userAttributes"":{}", errorTraceDataJson);
			}

			[Test]
			public void when_stack_trace_is_null_then_exception_is_not_thrown()
			{
				// ACT
				Assert.DoesNotThrow(
					() =>
						new ErrorTraceWireModel.ErrorTraceAttributesWireModel(EmptyDictionary, EmptyDictionary,
							EmptyDictionary, null));
			}
		}

		//}
	}
}
