// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Aggregators;
using NewRelic.Agent.Core.Attributes;
using NewRelic.Agent.Core.Errors;
using NewRelic.Agent.Core.Transformers.TransactionTransformer;
using NewRelic.Agent.Core.Utilities;
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

        private IAgentTimerService _agentTimerService;

        [SetUp]
        public void SetUp()
        {
            var configurationService = Mock.Create<IConfigurationService>();
            _configuration = Mock.Create<IConfiguration>();
            Mock.Arrange(() => _configuration.ErrorCollectorEnabled).Returns(true);
            Mock.Arrange(() => _configuration.CaptureCustomParameters).Returns(true);
            Mock.Arrange(() => configurationService.Configuration).Returns(_configuration);

            _agentTimerService = Mock.Create<IAgentTimerService>();
            _attribDefSvc = new AttributeDefinitionService((f) => new AttributeDefinitions(f));
            _errorTraceMaker = new ErrorTraceMaker(configurationService, _attribDefSvc, _agentTimerService);
            _errorTraceAggregator = Mock.Create<IErrorTraceAggregator>();
            _errorEventMaker = new ErrorEventMaker(_attribDefSvc, configurationService, _agentTimerService);
            _errorEventAggregator = Mock.Create<IErrorEventAggregator>();

            _customErrorDataTransformer = new CustomErrorDataTransformer(configurationService, _attribDefSvc, _errorTraceMaker, _errorTraceAggregator, _errorEventMaker, _errorEventAggregator);
        }

        [TearDown]
        public void TearDown()
        {
            _attribDefSvc.Dispose();
        }

        [Test]
        public void Transform_SendsErrorTraceToAggregator()
        {
            float priority = 0.5f;
            _customErrorDataTransformer.Transform(MakeError(), priority, string.Empty);

            Mock.Assert(() => _errorTraceAggregator.Collect(Arg.IsAny<ErrorTraceWireModel>()));
        }

        [Test]
        public void Transform_SendsErrorEventToAggregator()
        {
            float priority = 0.5f;

            _customErrorDataTransformer.Transform(MakeError(), priority, string.Empty);

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

            var errorData = new ErrorData(errorMsg, errorType, stackTrace, errorNoticedAt, errorCustomParameters, false, null);

            // ACT
            var errorTrace = _errorTraceMaker.GetErrorTrace( attribValues, errorData);

            //CAPTURE
            var userAttribs = errorTrace.Attributes.UserAttributes;

            //ASSERT
            NrAssert.Multiple
            (
                () => Assert.That(errorTrace.ExceptionClassName, Is.EqualTo(errorType)),
                () => Assert.That(errorTrace.Message, Is.EqualTo(errorMsg)),
                () => Assert.That(errorTrace.TimeStamp, Is.EqualTo(errorNoticedAt)),

                () => Assert.That(userAttribs, Has.Count.EqualTo(2)),
                () => Assert.That(userAttribs["TrxEventAttrib"], Is.EqualTo("TrxEventValue")),
                () => Assert.That(userAttribs["ErrorEventAttrib"], Is.EqualTo("ErrorEventValue"))
            );

        }

        [Test]
        public void Transform_DoesNotSendErrorTraceToAggregator_IfDisabledViaConfig()
        {
            Mock.Arrange(() => _configuration.ErrorCollectorEnabled).Returns(false);

            float priority = 0.5f;
            _customErrorDataTransformer.Transform(MakeError(), priority, string.Empty);

            Mock.Assert(() => _errorTraceAggregator.Collect(Arg.IsAny<ErrorTraceWireModel>()), Occurs.Never());
        }

        [Test]
        [TestCase("CustomUserId", true)]
        [TestCase("", false)]
        [TestCase(" ", false)]
        [TestCase(null, false)]
        public void Transform_SetsEndUserIdAttributeWhenNotNullOrWhitespace(string expectedUserId, bool attributeShouldExist)
        {
            _customErrorDataTransformer.Transform(MakeError(), 0.5f, expectedUserId);

            Mock.Assert(() => _errorTraceAggregator.Collect(
                Arg.Matches<ErrorTraceWireModel>(errorTraceWireModel =>
                    errorTraceWireModel.Attributes.AgentAttributes.ContainsKey(_attribDefs.EndUserId.Name) == attributeShouldExist)));

            Mock.Assert(() => _errorEventAggregator.Collect(
                Arg.Matches<ErrorEventWireModel>(errorEventWireModel =>
                    errorEventWireModel.AttributeValues.GetAttributeValuesDic(AttributeClassification.AgentAttributes).ContainsKey(_attribDefs.EndUserId.Name) == attributeShouldExist)));
        }

        private ErrorData MakeError(ReadOnlyDictionary<string, object> attributes = null)
        {
            return new ErrorData("error message", "error.type", null, System.DateTime.UtcNow, attributes, false, null);
        }
    }
}
