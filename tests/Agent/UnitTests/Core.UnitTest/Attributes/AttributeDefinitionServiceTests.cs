// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.AgentHealth;
using NewRelic.Agent.Core.Config;
using NewRelic.Agent.Core.Configuration;
using NewRelic.Agent.Core.Events;
using NewRelic.Agent.Core.Fixtures;
using NewRelic.Agent.Core.Metrics;
using NewRelic.Agent.Core.Segments;
using NewRelic.Agent.Core.SharedInterfaces;
using NewRelic.Agent.Core.SharedInterfaces.Web;
using NewRelic.Agent.Core.Transformers.TransactionTransformer;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Core.WireModels;
using NewRelic.Testing.Assertions;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.Attributes.Tests;

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
    private IBootstrapConfiguration _bootstrapConfiguration;

    private IEnvironment _environment;
    private IHttpRuntimeStatic _httpRuntimeStatic;
    private IProcessStatic _processStatic;
    private IConfigurationManagerStatic _configurationManagerStatic;
    private IDnsStatic _dnsStatic;
    private IAgentHealthReporter _agentHealthReporter;

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
        _bootstrapConfiguration = Mock.Create<IBootstrapConfiguration>();
        _agentHealthReporter = Mock.Create<IAgentHealthReporter>();
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
        _attribDefSvc = new AttributeDefinitionService((f, c, r) => new AttributeDefinitions(f, c, r), _agentHealthReporter);
        _transactionAttributeMaker = new TransactionAttributeMaker(_configurationService, _attribDefSvc);
    }

    private void UpdateConfig()
    {
        _configuration = new TestableDefaultConfiguration(_environment, _localConfig, _serverConfig, _runTimeConfiguration, _bootstrapConfiguration, _processStatic, _httpRuntimeStatic, _configurationManagerStatic, _dnsStatic);
        Mock.Arrange(() => _configurationService.Configuration).Returns(_configuration);
        EventBus<ConfigurationUpdatedEvent>.Publish(new ConfigurationUpdatedEvent(_configuration, ConfigurationUpdateSource.Local));
    }

    [TearDown]
    public void TearDown()
    {
        _configAutoResponder?.Dispose();
        _attribDefSvc.Dispose();
    }

    //        param     include                         exclude  
    [TestCase(null,     null,                           null,                       false   )]
    [TestCase(true,     null,                           null,                       false    )]
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
            () => Assert.That(attrib.IsAvailableForAny(AttributeDestinations.TransactionTrace), Is.EqualTo(expectCaptureRequestParams)),
            () => Assert.That(attrib.IsAvailableForAny(AttributeDestinations.ErrorTrace), Is.EqualTo(expectCaptureRequestParams)),
            () => Assert.That(attrib.IsAvailableForAny(AttributeDestinations.ErrorEvent), Is.EqualTo(expectCaptureRequestParams))
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
            () => Assert.That(attrib.IsAvailableForAny(AttributeDestinations.TransactionEvent), Is.EqualTo(expectCaptureRequestParams)),
            () => Assert.That(attrib.IsAvailableForAny(AttributeDestinations.SpanEvent), Is.EqualTo(expectCaptureRequestParams))
        );
    }

    //        input                  include                                                   exclude  
    [TestCase(new[] { "foo", "bar"},   null,                                                   null,                                   new[] { false, false })] // Don't expect to capture foo and bar.
    [TestCase(new[] { "foo", "bar"},   new[] { "request.headers.*" },                          null,                                   new[] { true, true })]
    [TestCase(new[] { "foo", "bar"},   new[] { "request.headers.foo" },                        null,                                   new[] { true, false })]  // Expect to capture foo, but don't expect to capture bar.
    [TestCase(new[] { "foo", "bar"},   new[] { "request.headers.*", "request.headers.foo" },   null,                                   new[] { true, true })]
    [TestCase(new[] { "foo", "bar"},   new[] { "request.headers.*" },                          new[] { "request.headers.foo" },        new[] { false, true })]
    [TestCase(new[] { "foo", "bar"},   null,                                                   new[] { "request.headers.foo" },        new[] { false, false })]
    public void RequestHeaderAttributeTests
    (
        string[] inputHeaders,
        string[] attributesInclude,
        string[] attributesExclude,
        bool[] expectCaptureRequestHeaders
    )
    {
        //Arrange

        if (attributesInclude != null)
        {
            _localConfig.attributes.include = new List<string>(attributesInclude);
        }

        if (attributesExclude != null)
        {
            _localConfig.attributes.exclude = new List<string>(attributesExclude);
        }

        UpdateConfig();

        for (var i = 0; i < inputHeaders.Length; i++)
        {
            var attrib = _attribDefs.GetRequestHeadersAttribute(inputHeaders[i]);

            NrAssert.Multiple
            (
                () => Assert.That(attrib.IsAvailableForAny(AttributeDestinations.TransactionEvent), Is.EqualTo(expectCaptureRequestHeaders[i])),
                () => Assert.That(attrib.IsAvailableForAny(AttributeDestinations.SpanEvent), Is.EqualTo(expectCaptureRequestHeaders[i])),
                () => Assert.That(attrib.IsAvailableForAny(AttributeDestinations.ErrorEvent), Is.EqualTo(expectCaptureRequestHeaders[i])),
                () => Assert.That(attrib.IsAvailableForAny(AttributeDestinations.ErrorTrace), Is.EqualTo(expectCaptureRequestHeaders[i])),
                () => Assert.That(attrib.IsAvailableForAny(AttributeDestinations.TransactionTrace), Is.EqualTo(expectCaptureRequestHeaders[i]))
            );
        }
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
            () => Assert.That(userAttribsDic[_lazyTest_AttribNameDtm], Is.Not.Null, "Lazy value that resolves to NOT NULL should be included"),
            () => Assert.That(userAttribsDic[_lazyTest_AttribNameDtm], Is.EqualTo(_lazyTest_ExpectedValDtmm)),
            () => Assert.That(userAttribsDic.ContainsKey(_lazyTest_AttribNameEmpty), Is.False, "Lazy value that resolves to NULL should NOT be included"),
            () => Assert.That(countAll, Is.EqualTo(1), "There should only be 1 value in the output collection")
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

    private void SetRequestAttributeCacheCapacity(int capacity)
    {
        _localConfig.appSettings = new List<configurationAdd>
        {
            new configurationAdd { key = "RequestAttributeCacheCapacity", value = capacity.ToString() }
        };
        UpdateConfig();
    }

    private AttributeDefinition<string, string> GetRequestAttribute(string kind, string name)
    {
        return kind == "parameters" ? _attribDefs.GetRequestParameterAttribute(name) : _attribDefs.GetRequestHeadersAttribute(name);
    }

    private int GetRequestAttributeCacheCount(string kind)
    {
        var attribDefs = (AttributeDefinitions)_attribDefs;
        return kind == "parameters" ? attribDefs.RequestParameterAttributeCacheCount : attribDefs.RequestHeadersAttributeCacheCount;
    }

    [TestCase("parameters")]
    [TestCase("headers")]
    public void RequestAttributeCache_ManyNewNames_CountStaysAtOrBelowConfiguredCapacity(string kind)
    {
        SetRequestAttributeCacheCapacity(5);

        for (var i = 0; i < 50; i++)
        {
            GetRequestAttribute(kind, $"name{i}");

            Assert.That(GetRequestAttributeCacheCount(kind), Is.LessThanOrEqualTo(5));
        }
    }

    [TestCase("parameters")]
    [TestCase("headers")]
    public void RequestAttributeCache_AfterFlush_NamesAreStillCaptured(string kind)
    {
        _localConfig.attributes.include.Add($"request.{kind}.*");
        SetRequestAttributeCacheCapacity(2);

        GetRequestAttribute(kind, "a");
        GetRequestAttribute(kind, "b");
        var flushingAttribute = GetRequestAttribute(kind, "c");
        var addedAgainAttribute = GetRequestAttribute(kind, "a");

        NrAssert.Multiple
        (
            () => Assert.That(flushingAttribute.IsAvailableForAny(AttributeDestinations.TransactionEvent), Is.True),
            () => Assert.That(addedAgainAttribute.IsAvailableForAny(AttributeDestinations.TransactionEvent), Is.True)
        );
    }

    [TestCase("parameters")]
    [TestCase("headers")]
    public void RequestAttributeCache_Flush_ReportsSupportabilityMetric(string kind)
    {
        var expectedMetric = kind == "parameters" ? MetricNames.SupportabilityRequestParameterAttributeCacheFlush : MetricNames.SupportabilityRequestHeaderAttributeCacheFlush;
        var otherMetric = kind == "parameters" ? MetricNames.SupportabilityRequestHeaderAttributeCacheFlush : MetricNames.SupportabilityRequestParameterAttributeCacheFlush;
        SetRequestAttributeCacheCapacity(1);

        GetRequestAttribute(kind, "a");
        GetRequestAttribute(kind, "b");

        NrAssert.Multiple
        (
            () => Mock.Assert(() => _agentHealthReporter.ReportSupportabilityCountMetric(expectedMetric, 1), Occurs.Once()),
            () => Mock.Assert(() => _agentHealthReporter.ReportSupportabilityCountMetric(otherMetric, 1), Occurs.Never())
        );
    }

    [TestCase("parameters")]
    [TestCase("headers")]
    public void RequestAttributeCache_LongName_ReturnsSharedDefinitionAndIsNotCached(string kind)
    {
        _localConfig.attributes.include.Add($"request.{kind}.*");
        UpdateConfig();

        var first = GetRequestAttribute(kind, new string('a', 256));
        var second = GetRequestAttribute(kind, new string('b', 256));

        NrAssert.Multiple
        (
            () => Assert.That(first.IsAvailableForAny(AttributeDestinations.TransactionEvent), Is.False),
            () => Assert.That(first.IsAvailableForAny(AttributeDestinations.SpanEvent), Is.False),
            () => Assert.That(first.IsAvailableForAny(AttributeDestinations.ErrorEvent), Is.False),
            () => Assert.That(first.IsAvailableForAny(AttributeDestinations.ErrorTrace), Is.False),
            () => Assert.That(first.IsAvailableForAny(AttributeDestinations.TransactionTrace), Is.False),
            () => Assert.That(second, Is.SameAs(first)),
            () => Assert.That(GetRequestAttributeCacheCount(kind), Is.EqualTo(0))
        );

        GetRequestAttribute(kind, new string('c', 255));

        Assert.That(GetRequestAttributeCacheCount(kind), Is.EqualTo(1));
    }

    [TestCase("parameters")]
    [TestCase("headers")]
    public void RequestAttributeCache_LongName_LogsOnceWithoutTheName(string kind)
    {
        var firstName = new string('a', 256);
        var secondName = new string('b', 256);
        const string logText = "A request attribute name longer than 255 characters was not captured";

        using (var logging = new TestUtilities.Logging())
        {
            GetRequestAttribute(kind, firstName);
            GetRequestAttribute(kind, secondName);

            NrAssert.Multiple
            (
                () => Assert.That(logging.DebugCount, Is.EqualTo(1)),
                () => Assert.That(logging.HasMessageThatContains(logText), Is.True),
                () => Assert.That(logging.HasMessageThatContains(firstName), Is.False),
                () => Assert.That(logging.HasMessageThatContains(secondName), Is.False)
            );
        }
    }

    [Test]
    public void RequestAttributeCache_ParameterAndHeaderCachesAreSeparate()
    {
        SetRequestAttributeCacheCapacity(2);

        _attribDefs.GetRequestParameterAttribute("a");
        _attribDefs.GetRequestParameterAttribute("b");

        Assert.That(GetRequestAttributeCacheCount("headers"), Is.EqualTo(0));

        _attribDefs.GetRequestHeadersAttribute("a");

        Assert.That(GetRequestAttributeCacheCount("parameters"), Is.EqualTo(2));
    }

    [TestCase("parameters")]
    [TestCase("headers")]
    public void RequestAttributeCache_ConfigurationChange_ResetsCacheAndCapacity(string kind)
    {
        SetRequestAttributeCacheCapacity(5);

        GetRequestAttribute(kind, "a");
        GetRequestAttribute(kind, "b");
        GetRequestAttribute(kind, "c");

        SetRequestAttributeCacheCapacity(2);

        Assert.That(GetRequestAttributeCacheCount(kind), Is.EqualTo(0));

        for (var i = 0; i < 3; i++)
        {
            GetRequestAttribute(kind, $"new{i}");

            Assert.That(GetRequestAttributeCacheCount(kind), Is.LessThanOrEqualTo(2));
        }
    }

    [Test]
    public void RequestAttributeCache_NullReporter_FlushDoesNotThrow()
    {
        var filter = new AttributeFilter(new AttributeFilter.Settings(_configuration));
        var attribDefs = new AttributeDefinitions(filter, 1);

        Assert.DoesNotThrow(() =>
        {
            attribDefs.GetRequestParameterAttribute("a");
            attribDefs.GetRequestParameterAttribute("b");
            attribDefs.GetRequestHeadersAttribute("a");
            attribDefs.GetRequestHeadersAttribute("b");
        });

        NrAssert.Multiple
        (
            () => Assert.That(attribDefs.RequestParameterAttributeCacheCount, Is.EqualTo(1)),
            () => Assert.That(attribDefs.RequestHeadersAttributeCacheCount, Is.EqualTo(1))
        );
    }
}