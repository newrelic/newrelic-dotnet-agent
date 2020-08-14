﻿// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NUnit.Framework;
using NewRelic.Testing.Assertions;
using System;
using NUnit.Framework.Internal;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Transformers.TransactionTransformer;
using NewRelic.Agent.Core.Config;
using NewRelic.Agent.Core.Configuration;
using NewRelic.SystemInterfaces;
using NewRelic.SystemInterfaces.Web;
using NewRelic.Agent.Core.Fixtures;
using Telerik.JustMock;
using NewRelic.Agent.Core.Configuration.UnitTest;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Core.Events;
using NewRelic.Agent.Core.WireModels;
using NewRelic.Agent.Core.Segments;
using System.Collections.Generic;

namespace NewRelic.Agent.Core.Attributes.Tests
{
    [TestFixture]
    public class AttributeDefinitionServiceTests
    {

        private IConfiguration _configuration;
        private IConfigurationService _configurationService;
        private ITransactionAttributeMaker _transactionAttributeMaker;
        private IAttributeDefinitionService _attribDefSvc;
        private IAttributeDefinitions _attribDefs => _attribDefSvc?.AttributeDefs;

        private configuration _localConfig;
        private ServerConfiguration _serverConfig;
        private RunTimeConfiguration _runTimeConfiguration;
        private SecurityPoliciesConfiguration _securityPoliciesConfiguration;

        private IEnvironment _environment;
        private IHttpRuntimeStatic _httpRuntimeStatic;
        private IProcessStatic _processStatic;
        private IConfigurationManagerStatic _configurationManagerStatic;
        private IDnsStatic _dnsStatic;

        private ConfigurationAutoResponder _configAutoResponder;

        private ITransactionMetricNameMaker _transactionMetricNameMaker;


        [SetUp]
        public void SetUp()
        {
            _environment = Mock.Create<IEnvironment>();

            Mock.Arrange(() => _environment.GetEnvironmentVariable(Arg.IsAny<string>()))
                .Returns(null as string);

            _processStatic = Mock.Create<IProcessStatic>();
            _httpRuntimeStatic = Mock.Create<IHttpRuntimeStatic>();
            _configurationManagerStatic = new ConfigurationManagerStaticMock();
            _dnsStatic = Mock.Create<IDnsStatic>();
            _securityPoliciesConfiguration = new SecurityPoliciesConfiguration();

            _runTimeConfiguration = new RunTimeConfiguration();
            _serverConfig = new ServerConfiguration();
            _localConfig = new configuration();

            _localConfig.crossApplicationTracingEnabled = true;
            _localConfig.attributes.enabled = true;
            _localConfig.spanEvents.enabled = true;
            _localConfig.distributedTracing.enabled = true;

            _configurationService = Mock.Create<IConfigurationService>();

            UpdateConfig();

            _configAutoResponder = new ConfigurationAutoResponder(_configuration);

            _transactionMetricNameMaker = Mock.Create<ITransactionMetricNameMaker>();
            _attribDefSvc = new AttributeDefinitionService((f) => new AttributeDefinitions(f));
            _transactionAttributeMaker = new TransactionAttributeMaker(_configurationService, _attribDefSvc);
        }

        private void UpdateConfig()
        {
            _configuration = new TestableDefaultConfiguration(_environment, _localConfig, _serverConfig, _runTimeConfiguration, _securityPoliciesConfiguration, _processStatic, _httpRuntimeStatic, _configurationManagerStatic, _dnsStatic);
            Mock.Arrange(() => _configurationService.Configuration).Returns(_configuration);
            EventBus<ConfigurationUpdatedEvent>.Publish(new ConfigurationUpdatedEvent(_configuration, ConfigurationUpdateSource.Local));
        }

        [TearDown]
        public void TearDown()
        {
            _configAutoResponder?.Dispose();
        }

        //        param     include                         exclude  
        [TestCase(null,     null,                           null,                       false   )]
        [TestCase(true,     null,                           null,                       true    )]
        [TestCase(false,    null,                           null,                       false   )]
        [TestCase(false,    "request.parameters.*",         null,                       true    )]
        [TestCase(null,     "request.parameters.*",         null,                       true    )]
        [TestCase(true,     "request.parameters.*",         "request.parameters.foo",   false   )]
        [TestCase(null,     "request.parameters.*",         "request.parameters.foo",   false   )]
        [TestCase(true,     null,                           "request.parameters.*",     false   )]
        public void RequestParametersTest_Traces
        (
            bool? configParamInclude,
            string attributesInclude,
            string attributesExclude,
            bool expectCaptureRequestParams
        )
        {
            //Arrange
            if (configParamInclude.HasValue)
            {
                _localConfig.requestParameters.enabled = configParamInclude.Value;
            }

            if(!string.IsNullOrWhiteSpace(attributesInclude))
            {
                _localConfig.attributes.include.Add(attributesInclude); 
            }

            if (!string.IsNullOrWhiteSpace(attributesExclude))
            {
                _localConfig.attributes.exclude.Add(attributesExclude);
            }

            UpdateConfig();


            var attrib = _attribDefs.GetRequestParameterAttribute("foo");

            NrAssert.Multiple
            (
                () => Assert.AreEqual(expectCaptureRequestParams, attrib.IsAvailableForAny(AttributeDestinations.TransactionTrace)),
                () => Assert.AreEqual(expectCaptureRequestParams, attrib.IsAvailableForAny(AttributeDestinations.ErrorTrace)),
                () => Assert.AreEqual(expectCaptureRequestParams, attrib.IsAvailableForAny(AttributeDestinations.ErrorEvent))
            );
        }


        //        param     include                         exclude  
        [TestCase(null,     null,                           null,                       false   )]
        [TestCase(true,     null,                           null,                       false   )]
        [TestCase(false,    null,                           null,                       false   )]
        [TestCase(false,    "request.parameters.*",         null,                       true    )]
        [TestCase(null,     "request.parameters.*",         null,                       true    )]
        [TestCase(true,     "request.parameters.*",         "request.parameters.foo",   false   )]
        [TestCase(null,     "request.parameters.*",         "request.parameters.foo",   false   )]
        [TestCase(true,     null,                           "request.parameters.*",     false   )]
        public void RequestParametersTest_Events
        (
            bool? configParamInclude,
            string attributesInclude,
            string attributesExclude,
            bool expectCaptureRequestParams
        )
        {
            //Arrange
            if (configParamInclude.HasValue)
            {
                _localConfig.requestParameters.enabled = configParamInclude.Value;
            }

            if(!string.IsNullOrWhiteSpace(attributesInclude))
            {
                _localConfig.attributes.include.Add(attributesInclude); 
            }

            if (!string.IsNullOrWhiteSpace(attributesExclude))
            {
                _localConfig.attributes.exclude.Add(attributesExclude);
            }

            UpdateConfig();


            var attrib = _attribDefs.GetRequestParameterAttribute("foo");

            NrAssert.Multiple
            (
                () => Assert.AreEqual(expectCaptureRequestParams, attrib.IsAvailableForAny(AttributeDestinations.TransactionEvent)),
                () => Assert.AreEqual(expectCaptureRequestParams, attrib.IsAvailableForAny(AttributeDestinations.SpanEvent))
            );
        }


        const string _lazyTest_AttribNameDtm = "dtmAttrib";
        private DateTime _lazyTest_AttribValDtm = DateTime.UtcNow;
        private object _lazyTest_ExpectedValDtmm => _lazyTest_AttribValDtm.ToString("o");
        const string _lazyTest_AttribNameEmpty = "emptyAttrib";

        private void LazyValueTest_SetAttribValues(IAttributeValueCollection attribValues)
        {
            var attribDefDtm = _attribDefs.GetCustomAttributeForTransaction(_lazyTest_AttribNameDtm);
            var attribDefEmpty = _attribDefs.GetCustomAttributeForTransaction(_lazyTest_AttribNameEmpty);

            //This is a lazy instantiation
            attribDefDtm.TrySetValue(attribValues, () => _lazyTest_AttribValDtm);
            attribDefEmpty.TrySetValue(attribValues, () => null);
        }

        private void LazyValueTest_Assertions(IAttributeValueCollection attribVals)
        {
            LazyValueTest_Assertions(attribVals.GetAttributeValuesDic(AttributeClassification.Intrinsics), attribVals.GetAttributeValuesDic(AttributeClassification.AgentAttributes), attribVals.GetAttributeValuesDic(AttributeClassification.UserAttributes));
        }

        private void LazyValueTest_Assertions(IDictionary<string,object> intrinsicAttribsDic, IDictionary<string,object> agentAttribsDic, IDictionary<string,object> userAttribsDic)
        {
            var countAll = (intrinsicAttribsDic?.Count).GetValueOrDefault(0)
                        + (agentAttribsDic?.Count).GetValueOrDefault(0)
                        + (userAttribsDic?.Count).GetValueOrDefault(0);

            NrAssert.Multiple
            (
                () => Assert.IsNotNull(userAttribsDic[_lazyTest_AttribNameDtm], "Lazy value that resolves to NOT NULL should be included"),
                () => Assert.AreEqual(_lazyTest_ExpectedValDtmm, userAttribsDic[_lazyTest_AttribNameDtm]),
                () => Assert.IsFalse(userAttribsDic.ContainsKey(_lazyTest_AttribNameEmpty), "Lazy value that resolves to NULL should NOT be included"),
                () => Assert.AreEqual(1, countAll, "There should only be 1 value in the output collection")
            );
        }

        [Test]
        public void LazyValueTest_TransactionEvents()
        {
            var attribValues = new AttributeValueCollection(AttributeValueCollection.AllTargetModelTypes);
            LazyValueTest_SetAttribValues(attribValues);

            var wireModel = new TransactionEventWireModel(attribValues, false, .5f);

            LazyValueTest_Assertions(wireModel.AttributeValues);
        }

        [Test]
        public void LazyValueTest_ErrorEvents()
        {
            var attribValues = new AttributeValueCollection(AttributeValueCollection.AllTargetModelTypes);
            LazyValueTest_SetAttribValues(attribValues);

            var wireModel = new ErrorEventWireModel(attribValues, false, .5f);

            LazyValueTest_Assertions(wireModel.AttributeValues);
        }

        [Test]
        public void LazyValueTest_TransactionSpanEvents()
        {
            var attribValues = new SpanAttributeValueCollection();
            LazyValueTest_SetAttribValues(attribValues);

            //This happens in the spanEventAggregator or SpanEventAggregatorForInfiniteTracing
            attribValues.MakeImmutable();

            LazyValueTest_Assertions(attribValues);
        }


        [Test]
        public void LazyValueTest_TransactionTrace()
        {
            var attribValues = new AttributeValueCollection(AttributeValueCollection.AllTargetModelTypes);
            LazyValueTest_SetAttribValues(attribValues);

            var wireModel = new TransactionTraceData.TransactionTraceAttributes(attribValues);

            LazyValueTest_Assertions(wireModel.Intrinsics,wireModel.AgentAttributes, wireModel.UserAttributes);
        }

        [Test]
        public void LazyValueTest_ErrorTrace()
        {
            var attribValues = new AttributeValueCollection(AttributeValueCollection.AllTargetModelTypes);
            LazyValueTest_SetAttribValues(attribValues);


            var wireModel = new ErrorTraceWireModel.ErrorTraceAttributesWireModel(attribValues, null);

            LazyValueTest_Assertions(wireModel.Intrinsics, wireModel.AgentAttributes, wireModel.UserAttributes);
        }

       




    }
}
