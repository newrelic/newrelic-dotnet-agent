using System;
using System.Collections.Generic;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Aggregators;
using NewRelic.Agent.Core.Attributes;
using NewRelic.Agent.Core.Errors;
using NewRelic.Agent.Core.Transformers.TransactionTransformer;
using NewRelic.Agent.Core.WireModels;
using NewRelic.Testing.Assertions;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.Transformers
{
    [TestFixture]
    public class CustomErrorDataTransformerTests
    {
        private CustomErrorDataTransformer _customErrorDataTransformer;

        private IConfiguration _configuration;

        private IAttributeDefinitionService _attribDefSvc;
        private IAttributeDefinitions _attribDefs => _attribDefSvc?.AttributeDefs;

        private IErrorTraceMaker _errorTraceMaker;

        private IErrorTraceAggregator _errorTraceAggregator;

        private IErrorEventMaker _errorEventMaker;

        private IErrorEventAggregator _errorEventAggregator;

        [SetUp]
        public void SetUp()
        {
            var configurationService = Mock.Create<IConfigurationService>();
            _configuration = Mock.Create<IConfiguration>();
            Mock.Arrange(() => _configuration.ErrorCollectorEnabled).Returns(true);
            Mock.Arrange(() => _configuration.CaptureCustomParameters).Returns(true);
            Mock.Arrange(() => configurationService.Configuration).Returns(_configuration);
            

            _attribDefSvc = new AttributeDefinitionService((f) => new AttributeDefinitions(f));
            _errorTraceMaker = new ErrorTraceMaker(configurationService);
            _errorTraceAggregator = Mock.Create<IErrorTraceAggregator>();
            _errorEventMaker = new ErrorEventMaker(_attribDefSvc);
            _errorEventAggregator = Mock.Create<IErrorEventAggregator>();

            _customErrorDataTransformer = new CustomErrorDataTransformer(configurationService, _attribDefSvc, _errorTraceMaker, _errorTraceAggregator, _errorEventMaker, _errorEventAggregator);
        }

        [Test]
        public void Transform_SendsErrorTraceToAggregator()
        {
            float priority = 0.5f;
            _customErrorDataTransformer.Transform(MakeError(), priority);

            Mock.Assert(() => _errorTraceAggregator.Collect(Arg.IsAny<ErrorTraceWireModel>()));
        }

        [Test]
        public void Transform_SendsErrorEventToAggregator()
        {
            float priority = 0.5f;

            _customErrorDataTransformer.Transform(MakeError(), priority);

            Mock.Assert(() => _errorEventAggregator.Collect(Arg.IsAny<ErrorEventWireModel>()));
        }

        

        [Test]
        public void Transform_FiltersAttributesBeforeSendingThemToErrorTraceMaker()
        {
            // ARRANGE
            var attribValues = new AttributeValueCollection(AttributeDestinations.TransactionEvent, AttributeDestinations.ErrorEvent, AttributeDestinations.ErrorTrace, AttributeDestinations.JavaScriptAgent); ;

            _attribDefs.GetCustomAttributeForCustomEvent("CustomEventAttrib").TrySetValue(attribValues, "CustomEventValue");        //CustomEvent
            _attribDefs.GetCustomAttributeForError("ErrorEventAttrib").TrySetValue(attribValues, "ErrorEventValue");                //Error Event and Trace
            _attribDefs.GetCustomAttributeForSpan("SpanEventAttrib").TrySetValue(attribValues, "SpanEventValue");                   //Span only
            _attribDefs.GetCustomAttributeForTransaction("TrxEventAttrib").TrySetValue(attribValues, "TrxEventValue");              //All Destiantions

            var errorCustomParameters = new ReadOnlyDictionary<string, object>(new Dictionary<string, object>() { { "ErrorCustomAttrib", "ErrorCustomValue" } });
            var errorNoticedAt = DateTime.Now;
            var errorMsg = "ErrorMessage";
            var errorType = "ErrorType";
            var stackTrace = "StackTrace";

            var errorData = new ErrorData(errorMsg, errorType, stackTrace, errorNoticedAt, errorCustomParameters);

            // ACT
            var errorTrace = _errorTraceMaker.GetErrorTrace( attribValues, errorData);

            //CAPTURE
            var userAttribs = errorTrace.Attributes.UserAttributes;

            //ASSERT
            NrAssert.Multiple
            (
                () => Assert.AreEqual(errorType, errorTrace.ExceptionClassName),
                () => Assert.AreEqual(errorMsg, errorTrace.Message),
                () => Assert.AreEqual(errorNoticedAt, errorTrace.TimeStamp),

                () => Assert.AreEqual(2, userAttribs.Count),
                () => Assert.AreEqual("TrxEventValue", userAttribs["TrxEventAttrib"]),
                () => Assert.AreEqual("ErrorEventValue", userAttribs["ErrorEventAttrib"])
            );

        }

        [Test]
        public void Transform_DoesNotSendErrorTraceToAggregator_IfDisabledViaConfig()
        {
            Mock.Arrange(() => _configuration.ErrorCollectorEnabled).Returns(false);

            float priority = 0.5f;
            _customErrorDataTransformer.Transform(MakeError(), priority);

            Mock.Assert(() => _errorTraceAggregator.Collect(Arg.IsAny<ErrorTraceWireModel>()), Occurs.Never());
        }

        private ErrorData MakeError(ReadOnlyDictionary<string, object> attributes = null)
        {
            return new ErrorData("error message", "error.type", null, System.DateTime.UtcNow, attributes);
        }
    }
}
