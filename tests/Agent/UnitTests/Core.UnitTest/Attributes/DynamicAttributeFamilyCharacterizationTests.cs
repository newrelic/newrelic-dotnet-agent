// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using NewRelic.Agent.Api.Experimental;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Aggregators;
using NewRelic.Agent.Core.CallStack;
using NewRelic.Agent.Core.Config;
using NewRelic.Agent.Core.Configuration;
using NewRelic.Agent.Core.Database;
using NewRelic.Agent.Core.DistributedTracing;
using NewRelic.Agent.Core.DistributedTracing.Samplers;
using NewRelic.Agent.Core.Errors;
using NewRelic.Agent.Core.Events;
using NewRelic.Agent.Core.Fixtures;
using NewRelic.Agent.Core.JsonConverters;
using NewRelic.Agent.Core.Segments;
using NewRelic.Agent.Core.SharedInterfaces;
using NewRelic.Agent.Core.SharedInterfaces.Web;
using NewRelic.Agent.Core.Spans;
using NewRelic.Agent.Core.Time;
using NewRelic.Agent.Core.Transactions;
using NewRelic.Agent.Core.Transformers;
using NewRelic.Agent.Core.Transformers.TransactionTransformer;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Core.WireModels;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Builders;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Data;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.Attributes.Tests;

/// <summary>
/// Pins the behavior of the six dynamic attribute families (transaction custom, span custom, error custom,
/// custom event, request parameters, request headers).
/// </summary>
[TestFixture]
public class DynamicAttributeFamilyCharacterizationTests
{
    public enum Family
    {
        TransactionCustom,
        SpanCustom,
        ErrorCustom,
        CustomEvent,
        RequestParameters,
        RequestHeaders
    }

    public enum Rule
    {
        Default,
        ExactExcludeOneDestination,
        Wildcard
    }

    public enum FilterMode
    {
        AttributesDisabled,
        TransactionEventAttributesDisabled,
        TransactionEventsDisabled,
        TransactionTraceAttributesDisabled,
        ErrorTraceAttributesDisabled,
        ErrorEventsDisabled,
        BrowserAttributesDisabled,
        SpanEventsDisabled,
        CustomEventsDisabled
    }

    private const string TransactionEvent = "TransactionEvent";
    private const string TransactionTrace = "TransactionTrace";
    private const string ErrorEvent = "ErrorEvent";
    private const string ErrorTrace = "ErrorTrace";
    private const string SpanEvent = "SpanEvent";
    private const string SpanEventSegment = "SpanEventSegment";
    private const string CustomEvent = "CustomEvent";
    private const string Browser = "Browser";
    private const string ErrorEventCustomError = "ErrorEventCustomError";
    private const string ErrorTraceCustomError = "ErrorTraceCustomError";

    private static readonly DateTime StartTime = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static KeyValuePair<string, object> Kv(string key, object value) => new KeyValuePair<string, object>(key, value);

    private static void IncludeRequestAttributes(configuration config)
    {
        config.attributes.include = new List<string> { "request.parameters.*", "request.headers.*" };
    }

    private static string RawName(Family family)
    {
        switch (family)
        {
            case Family.TransactionCustom: return "foo_tx";
            case Family.SpanCustom: return "foo_span";
            case Family.ErrorCustom: return "foo_err";
            case Family.CustomEvent: return "foo_event";
            case Family.RequestParameters: return "foo_param";
            default: return "foo_header";
        }
    }

    private static string OutputName(Family family, string rawName)
    {
        switch (family)
        {
            case Family.RequestParameters: return "request.parameters." + rawName;
            case Family.RequestHeaders: return "request.headers." + rawName.ToLowerInvariant();
            default: return rawName;
        }
    }

    private static string OutputName(Family family) => OutputName(family, RawName(family));

    private static string Normalize(string destinations)
    {
        return string.Join(",", destinations.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).OrderBy(d => d, StringComparer.Ordinal));
    }

    // Destinations that each family reaches when browser attributes and request attributes are on.
    private static string ReachableDestinations(Family family)
    {
        switch (family)
        {
            case Family.TransactionCustom: return Normalize($"{TransactionEvent},{TransactionTrace},{ErrorEvent},{ErrorTrace},{SpanEvent},{Browser}");
            case Family.SpanCustom: return Normalize(SpanEventSegment);
            case Family.ErrorCustom: return Normalize($"{ErrorEvent},{ErrorTrace},{ErrorEventCustomError},{ErrorTraceCustomError}");
            case Family.CustomEvent: return Normalize(CustomEvent);
            default: return Normalize($"{TransactionEvent},{TransactionTrace},{ErrorEvent},{ErrorTrace},{SpanEvent}");
        }
    }

    private static string PrimaryDestination(Family family)
    {
        switch (family)
        {
            case Family.SpanCustom: return SpanEventSegment;
            case Family.ErrorCustom: return ErrorEvent;
            case Family.CustomEvent: return CustomEvent;
            default: return TransactionEvent;
        }
    }

    private static string FirstConfigurableDestination(Family family)
    {
        switch (family)
        {
            case Family.SpanCustom: return SpanEventSegment;
            case Family.ErrorCustom: return ErrorTrace;
            case Family.CustomEvent: return CustomEvent;
            default: return TransactionEvent;
        }
    }

    private static void ExcludeAtDestination(configuration config, string destination, string name)
    {
        var excludes = new List<string> { name };
        switch (destination)
        {
            case TransactionEvent: config.transactionEvents.attributes.exclude = excludes; break;
            case TransactionTrace: config.transactionTracer.attributes.exclude = excludes; break;
            case ErrorTrace:
            case ErrorEvent: config.errorCollector.attributes.exclude = excludes; break;
            case Browser: config.browserMonitoring.attributes.exclude = excludes; break;
            case SpanEvent:
            case SpanEventSegment: config.spanEvents.attributes.exclude = excludes; break;
            case CustomEvent: config.customEvents.attributes.exclude = excludes; break;
            default: throw new ArgumentOutOfRangeException(nameof(destination), destination, null);
        }
    }

    private static Action<configuration> ConfigFor(Family family, Rule rule)
    {
        return config =>
        {
            config.browserMonitoring.attributes.enabled = true;
            var requestFamily = family == Family.RequestParameters || family == Family.RequestHeaders;

            switch (rule)
            {
                case Rule.ExactExcludeOneDestination:
                    if (requestFamily)
                    {
                        IncludeRequestAttributes(config);
                    }
                    ExcludeAtDestination(config, FirstConfigurableDestination(family), OutputName(family));
                    break;
                case Rule.Wildcard:
                    if (requestFamily)
                    {
                        IncludeRequestAttributes(config);
                    }
                    else
                    {
                        config.attributes.exclude = new List<string> { "fo*" };
                    }
                    break;
            }
        };
    }

    private static void ApplyFilterMode(configuration config, FilterMode mode)
    {
        switch (mode)
        {
            case FilterMode.AttributesDisabled: config.attributes.enabled = false; break;
            case FilterMode.TransactionEventAttributesDisabled: config.transactionEvents.attributes.enabled = false; break;
            case FilterMode.TransactionEventsDisabled: config.transactionEvents.enabled = false; break;
            case FilterMode.TransactionTraceAttributesDisabled: config.transactionTracer.attributes.enabled = false; break;
            case FilterMode.ErrorTraceAttributesDisabled: config.errorCollector.attributes.enabled = false; break;
            case FilterMode.ErrorEventsDisabled: config.errorCollector.captureEvents = false; break;
            case FilterMode.BrowserAttributesDisabled: config.browserMonitoring.attributes.enabled = false; break;
            case FilterMode.SpanEventsDisabled: config.spanEvents.enabled = false; break;
            case FilterMode.CustomEventsDisabled: config.customEvents.enabled = false; break;
        }
    }

    // The destinations that a filter mode removes. A null result means every destination.
    private static string[] DestinationsRemovedBy(FilterMode mode)
    {
        switch (mode)
        {
            case FilterMode.AttributesDisabled: return null;
            case FilterMode.TransactionEventAttributesDisabled: return new[] { TransactionEvent };
            case FilterMode.TransactionEventsDisabled: return new string[0];
            case FilterMode.TransactionTraceAttributesDisabled: return new[] { TransactionTrace };
            case FilterMode.ErrorTraceAttributesDisabled: return new[] { ErrorTrace, ErrorTraceCustomError };
            case FilterMode.ErrorEventsDisabled: return new[] { ErrorEvent, ErrorEventCustomError };
            case FilterMode.BrowserAttributesDisabled: return new[] { Browser };
            case FilterMode.SpanEventsDisabled: return new[] { SpanEvent, SpanEventSegment };
            default: return new[] { CustomEvent };
        }
    }

    public static IEnumerable<TestCaseData> FilterModeCases()
    {
        foreach (Family family in Enum.GetValues(typeof(Family)))
        {
            foreach (FilterMode mode in Enum.GetValues(typeof(FilterMode)))
            {
                yield return new TestCaseData(family, mode).SetName($"FilterModes({family},{mode})");
            }
        }
    }

    #region Serialization helpers

    private static string Serialize(Action<JsonWriter> write)
    {
        var builder = new StringBuilder();
        using (var writer = new JsonTextWriter(new StringWriter(builder)))
        {
            write(writer);
            writer.Flush();
        }

        return builder.ToString();
    }

    // Returns each object that is a direct child of a top-level array, in order.
    private static List<string> TopLevelObjects(string json)
    {
        var result = new List<string>();
        var depth = 0;
        var start = -1;
        var inString = false;

        for (var i = 0; i < json.Length; i++)
        {
            var c = json[i];
            if (inString)
            {
                if (c == '\\')
                {
                    i++;
                }
                else if (c == '"')
                {
                    inString = false;
                }

                continue;
            }

            switch (c)
            {
                case '"':
                    inString = true;
                    break;
                case '{':
                    if (depth == 1)
                    {
                        start = i;
                    }
                    depth++;
                    break;
                case '[':
                    depth++;
                    break;
                case '}':
                    depth--;
                    if (depth == 1)
                    {
                        result.Add(json.Substring(start, i - start + 1));
                    }
                    break;
                case ']':
                    depth--;
                    break;
            }
        }

        return result;
    }

    // Reads the property names and compact values of a JSON object. A repeated name is kept as it appears.
    private static List<KeyValuePair<string, string>> Pairs(string jsonObject)
    {
        var pairs = new List<KeyValuePair<string, string>>();
        var reader = new JsonTextReader(new StringReader(jsonObject));
        while (reader.Read())
        {
            if (reader.TokenType == JsonToken.PropertyName && reader.Depth == 1)
            {
                var name = (string)reader.Value;
                reader.Read();
                var token = JToken.ReadFrom(reader);
                pairs.Add(new KeyValuePair<string, string>(name, token.ToString(Formatting.None)));
            }
        }

        return pairs;
    }

    private static string[] EventObjects(IEventWireModel model)
    {
        var json = Serialize(w => new EventWireModelSerializer().WriteJson(w, model, JsonSerializer.CreateDefault()));
        var objects = TopLevelObjects(json);
        return new[] { objects[1], objects[2] };
    }

    private static string[] SpanObjects(ISpanEventWireModel model, int spanIndex = 0)
    {
        var json = Serialize(w => new SpanEventWireModelSerializer().WriteJson(w, model, JsonSerializer.CreateDefault()));
        var objects = TopLevelObjects(json);
        return new[] { objects[spanIndex * 3 + 1], objects[spanIndex * 3 + 2] };
    }

    private static string[] TraceObjects(object attributesWireModel)
    {
        var parsed = JObject.Parse(JsonConvert.SerializeObject(attributesWireModel));
        return new[] { parsed["userAttributes"].ToString(Formatting.None), parsed["agentAttributes"].ToString(Formatting.None) };
    }

    private static string[] BrowserObjects(IAttributeValueCollection attributes)
    {
        var json = Serialize(w => new BrowserEventWireModelSerializer().WriteJson(w, attributes, JsonSerializer.CreateDefault()));
        if (string.IsNullOrEmpty(json))
        {
            return new[] { "{}", "{}" };
        }

        var parsed = JObject.Parse(json);
        return new[] { (parsed["u"] ?? new JObject()).ToString(Formatting.None), (parsed["a"] ?? new JObject()).ToString(Formatting.None) };
    }

    #endregion

    private sealed class Inputs
    {
        public readonly List<KeyValuePair<string, object>> TransactionCustom = new List<KeyValuePair<string, object>>();
        public readonly List<KeyValuePair<string, object>> SpanCustom = new List<KeyValuePair<string, object>>();
        public readonly Dictionary<string, object> ErrorCustom = new Dictionary<string, object>();
        public readonly List<KeyValuePair<string, object>> CustomEvent = new List<KeyValuePair<string, object>>();
        public readonly List<KeyValuePair<string, string>> RequestParameters = new List<KeyValuePair<string, string>>();
        public readonly List<KeyValuePair<string, string>> RequestHeaders = new List<KeyValuePair<string, string>>();

        public Inputs Add(Family family, string name, string value)
        {
            switch (family)
            {
                case Family.TransactionCustom: TransactionCustom.Add(Kv(name, value)); break;
                case Family.SpanCustom: SpanCustom.Add(Kv(name, value)); break;
                case Family.ErrorCustom: ErrorCustom[name] = value; break;
                case Family.CustomEvent: CustomEvent.Add(Kv(name, value)); break;
                case Family.RequestParameters: RequestParameters.Add(new KeyValuePair<string, string>(name, value)); break;
                default: RequestHeaders.Add(new KeyValuePair<string, string>(name, value)); break;
            }

            return this;
        }

        public static Inputs AllFamilies()
        {
            var inputs = new Inputs();
            inputs.TransactionCustom.Add(Kv(RawName(Family.TransactionCustom), "1"));
            inputs.SpanCustom.Add(Kv(RawName(Family.SpanCustom), "1"));
            inputs.ErrorCustom[RawName(Family.ErrorCustom)] = "1";
            inputs.CustomEvent.Add(Kv(RawName(Family.CustomEvent), "1"));
            inputs.RequestParameters.Add(new KeyValuePair<string, string>(RawName(Family.RequestParameters), "1"));
            inputs.RequestHeaders.Add(new KeyValuePair<string, string>(RawName(Family.RequestHeaders), "1"));
            return inputs;
        }
    }

    private sealed class Outputs
    {
        private readonly Dictionary<string, string[]> _objects = new Dictionary<string, string[]>();

        public IAttributeValueCollection Attributes { get; set; }

        public void Add(string destination, string[] userAndAgent)
        {
            _objects[destination] = userAndAgent;
        }

        public string User(string destination) => _objects[destination][0];

        public string Agent(string destination) => _objects[destination][1];

        public List<string> Names(string destination)
        {
            return Pairs(User(destination)).Concat(Pairs(Agent(destination))).Select(p => p.Key).ToList();
        }

        // The compact JSON value of every property with this name, user and agent objects together, in output order.
        public List<string> Values(string destination, string outputName)
        {
            return Pairs(User(destination)).Concat(Pairs(Agent(destination))).Where(p => p.Key == outputName).Select(p => p.Value).ToList();
        }

        public string Present(string outputName)
        {
            return string.Join(",", _objects.Keys.Where(d => Names(d).Contains(outputName)).OrderBy(d => d, StringComparer.Ordinal));
        }

        public string Present(Family family) => Present(OutputName(family));

        public bool AnyNameStartsWith(string prefix) => _objects.Keys.Any(d => Names(d).Any(n => n.StartsWith(prefix, StringComparison.Ordinal)));
    }

    private sealed class Rig : IDisposable
    {
        private readonly configuration _localConfig = new configuration();
        private readonly IEnvironment _environment = Mock.Create<IEnvironment>();
        private readonly IProcessStatic _processStatic = Mock.Create<IProcessStatic>();
        private readonly IHttpRuntimeStatic _httpRuntimeStatic = Mock.Create<IHttpRuntimeStatic>();
        private readonly IConfigurationManagerStatic _configurationManagerStatic = Mock.Create<IConfigurationManagerStatic>();
        private readonly IDnsStatic _dnsStatic = Mock.Create<IDnsStatic>();
        private readonly IBootstrapConfiguration _bootstrapConfiguration = Mock.Create<IBootstrapConfiguration>();
        private readonly IConfigurationService _configurationService = Mock.Create<IConfigurationService>();
        private readonly IDatabaseService _databaseService = new DatabaseService();
        private readonly ConfigurationAutoResponder _autoResponder;
        private readonly AttributeDefinitionService _attribDefSvc;
        private readonly TransactionAttributeMaker _attributeMaker;
        private IConfiguration _configuration;

        public IAttributeDefinitions AttribDefs => _attribDefSvc.AttributeDefs;

        public IConfiguration Configuration => _configuration;

        public Rig(Action<configuration> configure = null)
        {
            _localConfig.distributedTracing.enabled = true;
            _localConfig.processHost.displayName = "test-host";
            configure?.Invoke(_localConfig);

            _configuration = BuildConfiguration();
            Mock.Arrange(() => _configurationService.Configuration).Returns(() => _configuration);

            _autoResponder = new ConfigurationAutoResponder(_configuration);
            _attribDefSvc = new AttributeDefinitionService(f => new AttributeDefinitions(f));
            _attributeMaker = new TransactionAttributeMaker(_configurationService, _attribDefSvc);
        }

        private IConfiguration BuildConfiguration()
        {
            return new TestableDefaultConfiguration(_environment, _localConfig, new ServerConfiguration(), new RunTimeConfiguration(), _bootstrapConfiguration, _processStatic, _httpRuntimeStatic, _configurationManagerStatic, _dnsStatic);
        }

        public void Reconfigure(Action<configuration> configure)
        {
            configure(_localConfig);
            _configuration = BuildConfiguration();
            _autoResponder.Configuration = _configuration;
            EventBus<ConfigurationUpdatedEvent>.Publish(new ConfigurationUpdatedEvent(_configuration, ConfigurationUpdateSource.Local));
        }

        public void Dispose()
        {
            _autoResponder.Dispose();
            _attribDefSvc.Dispose();
            _databaseService.Dispose();
        }

        public Transaction NewTransaction()
        {
            var timer = Mock.Create<ISimpleTimer>();
            Mock.Arrange(() => timer.Duration).Returns(TimeSpan.FromMilliseconds(500));

            return new Transaction(_configuration, TransactionName.ForOtherTransaction("category", "name"), timer, StartTime, Mock.Create<ICallStackManager>(), _databaseService, 0.5f, Mock.Create<IDatabaseStatementParser>(), Mock.Create<IDistributedTracePayloadHandler>(), new ErrorService(_configurationService), AttribDefs, Mock.Create<ISamplerService>());
        }

        public Segment NewSegment()
        {
            var segmentState = Mock.Create<ITransactionSegmentState>();
            Mock.Arrange(() => segmentState.AttribDefs).Returns(() => AttribDefs);
            Mock.Arrange(() => segmentState.ErrorService).Returns(new ErrorService(_configurationService));

            return new Segment(segmentState, new MethodCallData("type", "method", 1));
        }

        public static ErrorData NewErrorData(IDictionary<string, object> customAttributes)
        {
            return new ErrorData("error message", "ErrorType", "stack trace", StartTime, new ReadOnlyDictionary<string, object>(customAttributes), false, null);
        }

        public CustomEventWireModel RunCustomEvent(IEnumerable<KeyValuePair<string, object>> attributes)
        {
            CustomEventWireModel collected = null;
            var aggregator = Mock.Create<ICustomEventAggregator>();
            Mock.Arrange(() => aggregator.Collect(Arg.IsAny<CustomEventWireModel>())).DoInstead<CustomEventWireModel>(e => collected = e);

            new CustomEventTransformer(_configurationService, aggregator, _attribDefSvc).Transform("TestEvent", attributes, 0.5f);

            return collected;
        }

        public void RunCustomError(ErrorData errorData, out ErrorTraceWireModel errorTrace, out ErrorEventWireModel errorEvent)
        {
            ErrorTraceWireModel collectedTrace = null;
            ErrorEventWireModel collectedEvent = null;
            var traceAggregator = Mock.Create<IErrorTraceAggregator>();
            var eventAggregator = Mock.Create<IErrorEventAggregator>();
            Mock.Arrange(() => traceAggregator.Collect(Arg.IsAny<ErrorTraceWireModel>())).DoInstead<ErrorTraceWireModel>(e => collectedTrace = e);
            Mock.Arrange(() => eventAggregator.Collect(Arg.IsAny<ErrorEventWireModel>())).DoInstead<ErrorEventWireModel>(e => collectedEvent = e);
            var timerService = Mock.Create<IAgentTimerService>();

            var transformer = new CustomErrorDataTransformer(_configurationService, _attribDefSvc,
                new ErrorTraceMaker(_configurationService, _attribDefSvc, timerService), traceAggregator,
                new ErrorEventMaker(_attribDefSvc, _configurationService, timerService), eventAggregator);
            transformer.Transform(errorData, 0.5f, string.Empty);

            errorTrace = collectedTrace;
            errorEvent = collectedEvent;
        }

        public ImmutableTransaction Immutable(Transaction transaction) => transaction.ConvertToImmutableTransaction();

        public IAttributeValueCollection MakeAttributes(ImmutableTransaction immutable)
        {
            var metricName = new TransactionMetricName("OtherTransaction", "category/name");
            return _attributeMaker.GetAttributes(immutable, metricName, null, TimeSpan.FromSeconds(1), new TransactionMetricStatsCollection(metricName));
        }

        public IList<ISpanEventWireModel> MakeSpans(ImmutableTransaction immutable, IAttributeValueCollection attributes)
        {
            return new SpanEventMaker(_attribDefSvc, _configurationService)
                .GetSpanEvents(immutable, "OtherTransaction/category/name", attributes)
                .ToList();
        }

        public IAttributeValueCollection MakeBrowserAttributes(ImmutableTransaction immutable)
        {
            var browserAttributes = new AttributeValueCollection(AttributeDestinations.JavaScriptAgent);
            _attributeMaker.SetUserAndAgentAttributes(browserAttributes, immutable.TransactionMetadata);
            return browserAttributes;
        }

        public string[] TransactionEventObjects(Transaction transaction)
        {
            return EventObjects(new TransactionEventWireModel(MakeAttributes(Immutable(transaction)), false, 0.5f));
        }

        public Outputs Run(Inputs inputs)
        {
            var outputs = new Outputs();
            var transaction = NewTransaction();
            var segment = NewSegment();

            foreach (var attribute in inputs.TransactionCustom)
            {
                transaction.AddCustomAttribute(attribute.Key, attribute.Value);
            }

            transaction.SetRequestParameters(inputs.RequestParameters);

            foreach (var header in inputs.RequestHeaders)
            {
                transaction.SetRequestHeaders(new Dictionary<string, string> { { header.Key, header.Value } }, new[] { header.Key }, (d, k) => d[k]);
            }

            foreach (var attribute in inputs.SpanCustom)
            {
                segment.AddCustomAttribute(attribute.Key, attribute.Value);
            }

            transaction.Add(segment);

            if (inputs.ErrorCustom.Count > 0)
            {
                transaction.NoticeError(NewErrorData(inputs.ErrorCustom));
            }

            var immutable = Immutable(transaction);
            var attributes = MakeAttributes(immutable);
            var spans = MakeSpans(immutable, attributes);

            outputs.Attributes = attributes;

            outputs.Add(TransactionEvent, EventObjects(new TransactionEventWireModel(attributes, false, 0.5f)));
            outputs.Add(ErrorEvent, EventObjects(new ErrorEventWireModel(attributes, false, 0.5f)));
            outputs.Add(TransactionTrace, TraceObjects(new TransactionTraceData.TransactionTraceAttributes(attributes)));
            outputs.Add(ErrorTrace, TraceObjects(new ErrorTraceWireModel.ErrorTraceAttributesWireModel(attributes)));
            outputs.Add(SpanEvent, SpanObjects(spans[0]));
            outputs.Add(SpanEventSegment, SpanObjects(spans[1]));
            outputs.Add(Browser, BrowserObjects(MakeBrowserAttributes(immutable)));

            if (inputs.CustomEvent.Count > 0)
            {
                var customEvent = RunCustomEvent(inputs.CustomEvent);
                if (customEvent != null)
                {
                    outputs.Add(CustomEvent, EventObjects(customEvent));
                }
            }

            if (inputs.ErrorCustom.Count > 0)
            {
                RunCustomError(NewErrorData(inputs.ErrorCustom), out var errorTrace, out var errorEvent);
                if (errorTrace != null)
                {
                    outputs.Add(ErrorTraceCustomError, TraceObjects(errorTrace.Attributes));
                }

                if (errorEvent != null)
                {
                    outputs.Add(ErrorEventCustomError, EventObjects(errorEvent));
                }
            }

            return outputs;
        }
    }

    // Applies a batch of attributes to one family through its entry point and exposes the collection that holds them.
    private sealed class Driver
    {
        private readonly Family _family;
        private readonly Rig _rig;
        private readonly Transaction _transaction;
        private readonly Segment _segment;
        private IAttributeValueCollection _collection;

        public Driver(Rig rig, Family family)
        {
            _rig = rig;
            _family = family;
            _transaction = rig.NewTransaction();
            _segment = rig.NewSegment();
        }

        public AttributeClassification Classification
        {
            get
            {
                var requestFamily = _family == Family.RequestParameters || _family == Family.RequestHeaders;
                return requestFamily ? AttributeClassification.AgentAttributes : AttributeClassification.UserAttributes;
            }
        }

        public IAttributeValueCollection Collection
        {
            get
            {
                switch (_family)
                {
                    case Family.SpanCustom: return _segment.GetAttributeValues();
                    case Family.ErrorCustom:
                    case Family.CustomEvent: return _collection;
                    default: return _transaction.TransactionMetadata.UserAndRequestAttributes;
                }
            }
        }

        public Dictionary<string, object> Values => new Dictionary<string, object>(Collection.GetAttributeValuesDic(Classification));

        public int ClassificationCount => Collection.GetAttributeValues(Classification).Count();

        public string Name(string rawName) => OutputName(_family, rawName);

        public void Apply(IReadOnlyList<KeyValuePair<string, object>> items)
        {
            switch (_family)
            {
                case Family.TransactionCustom:
                    foreach (var item in items)
                    {
                        _transaction.AddCustomAttribute(item.Key, item.Value);
                    }
                    break;
                case Family.SpanCustom:
                    foreach (var item in items)
                    {
                        _segment.AddCustomAttribute(item.Key, item.Value);
                    }
                    break;
                case Family.ErrorCustom:
                    var errorAttributes = new Dictionary<string, object>();
                    foreach (var item in items)
                    {
                        errorAttributes[item.Key] = item.Value;
                    }
                    _rig.RunCustomError(Rig.NewErrorData(errorAttributes), out _, out var errorEvent);
                    _collection = errorEvent.AttributeValues;
                    break;
                case Family.CustomEvent:
                    _collection = _rig.RunCustomEvent(items).AttributeValues;
                    break;
                case Family.RequestParameters:
                    _transaction.SetRequestParameters(items.Select(i => new KeyValuePair<string, string>(i.Key, (string)i.Value)).ToList());
                    break;
                default:
                    foreach (var item in items)
                    {
                        _transaction.SetRequestHeaders(new Dictionary<string, string> { { item.Key, (string)item.Value } }, new[] { item.Key }, (d, k) => d[k]);
                    }
                    break;
            }
        }
    }

    #region Limits and last write wins

    [TestCase(Family.TransactionCustom)]
    [TestCase(Family.SpanCustom)]
    [TestCase(Family.CustomEvent)]
    [TestCase(Family.RequestParameters)]
    [TestCase(Family.RequestHeaders)]
    public void LastWriteWins_AndOverwriteDoesNotCount(Family family)
    {
        using var rig = new Rig(IncludeRequestAttributes);
        var driver = new Driver(rig, family);
        var name = driver.Name("foo");

        driver.Apply(new[] { Kv("foo", "1"), Kv("foo", "2") });

        var inputs = new Inputs().Add(family, "foo", "1").Add(family, "foo", "2");
        var outputs = rig.Run(inputs);

        Assert.Multiple(() =>
        {
            Assert.That(driver.Values[name], Is.EqualTo("2"));
            Assert.That(driver.ClassificationCount, Is.EqualTo(1));
            Assert.That(outputs.Values(PrimaryDestination(family), name), Is.EqualTo(new[] { "\"2\"" }));
        });
    }

    [TestCase(Family.TransactionCustom)]
    [TestCase(Family.SpanCustom)]
    [TestCase(Family.ErrorCustom)]
    [TestCase(Family.CustomEvent)]
    public void UserLimit_64th_Accepted_65th_Rejected(Family family)
    {
        using var rig = new Rig();
        var driver = new Driver(rig, family);
        var items = Enumerable.Range(1, 65).Select(i => Kv("name" + i, "value" + i)).ToList();
        items.Add(Kv("name1", "changed"));

        driver.Apply(items);
        var values = driver.Values;

        Assert.Multiple(() =>
        {
            Assert.That(driver.ClassificationCount, Is.EqualTo(64));
            Assert.That(values["name64"], Is.EqualTo("value64"));
            Assert.That(values.ContainsKey("name65"), Is.False);

            // Error custom input is a dictionary, so it cannot repeat a name.
            if (family != Family.ErrorCustom)
            {
                Assert.That(values["name1"], Is.EqualTo("changed"));
            }
        });
    }

    [Test]
    public void TotalLimit_255()
    {
        using var rig = new Rig(IncludeRequestAttributes);
        var driver = new Driver(rig, Family.RequestParameters);
        var items = Enumerable.Range(1, 256).Select(i => Kv("p" + i, "value" + i)).ToList();

        driver.Apply(items);
        var countAfterFill = driver.Collection.Count;
        var valuesAfterFill = driver.Values;

        driver.Apply(new[] { Kv("p1", "changed") });

        Assert.Multiple(() =>
        {
            Assert.That(countAfterFill, Is.EqualTo(255));
            Assert.That(valuesAfterFill.ContainsKey("request.parameters.p255"), Is.True);
            Assert.That(valuesAfterFill.ContainsKey("request.parameters.p256"), Is.False);
            Assert.That(driver.Collection.Count, Is.EqualTo(255));
            Assert.That(driver.Values["request.parameters.p1"], Is.EqualTo("changed"));
        });
    }

    [Test]
    public void UserLimit_IsSharedAcrossFamilies()
    {
        using var rig = new Rig();
        var inputs = new Inputs();
        for (var i = 0; i < 40; i++)
        {
            inputs.TransactionCustom.Add(Kv("tx" + i, "value"));
            inputs.ErrorCustom["err" + i] = "value";
        }

        var outputs = rig.Run(inputs);

        // Error custom attributes are set first, so the transaction custom attributes lose the last 16 slots.
        var expectedErrorNames = Enumerable.Range(0, 40).Select(i => "err" + i).Concat(Enumerable.Range(0, 24).Select(i => "tx" + i)).ToList();
        var expectedTransactionNames = Enumerable.Range(0, 24).Select(i => "tx" + i).ToList();
        var userNames = outputs.Attributes.GetAttributeValuesDic(AttributeClassification.UserAttributes).Keys;
        var errorEventUserNames = Pairs(outputs.User(ErrorEvent)).Select(p => p.Key).ToList();
        var transactionEventUserNames = Pairs(outputs.User(TransactionEvent)).Select(p => p.Key).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(userNames, Is.EquivalentTo(expectedErrorNames));
            Assert.That(errorEventUserNames, Is.EquivalentTo(expectedErrorNames));
            Assert.That(errorEventUserNames, Has.Count.EqualTo(64));
            Assert.That(transactionEventUserNames, Is.EquivalentTo(expectedTransactionNames));
        });
    }

    #endregion

    #region Include and exclude rules and filter modes

    [TestCase(Family.TransactionCustom, Rule.Default, "TransactionEvent,TransactionTrace,ErrorEvent,ErrorTrace,SpanEvent,Browser")]
    [TestCase(Family.TransactionCustom, Rule.ExactExcludeOneDestination, "TransactionTrace,ErrorEvent,ErrorTrace,SpanEvent,Browser")]
    [TestCase(Family.TransactionCustom, Rule.Wildcard, "")]
    [TestCase(Family.SpanCustom, Rule.Default, "SpanEventSegment")]
    [TestCase(Family.SpanCustom, Rule.ExactExcludeOneDestination, "")]
    [TestCase(Family.SpanCustom, Rule.Wildcard, "")]
    [TestCase(Family.ErrorCustom, Rule.Default, "ErrorEvent,ErrorTrace,ErrorEventCustomError,ErrorTraceCustomError")]
    [TestCase(Family.ErrorCustom, Rule.ExactExcludeOneDestination, "")]
    [TestCase(Family.ErrorCustom, Rule.Wildcard, "")]
    [TestCase(Family.CustomEvent, Rule.Default, "CustomEvent")]
    [TestCase(Family.CustomEvent, Rule.ExactExcludeOneDestination, "CustomEvent")]
    [TestCase(Family.CustomEvent, Rule.Wildcard, "")]
    [TestCase(Family.RequestParameters, Rule.Default, "")]
    [TestCase(Family.RequestParameters, Rule.ExactExcludeOneDestination, "TransactionTrace,ErrorEvent,ErrorTrace,SpanEvent")]
    [TestCase(Family.RequestParameters, Rule.Wildcard, "TransactionEvent,TransactionTrace,ErrorEvent,ErrorTrace,SpanEvent")]
    [TestCase(Family.RequestHeaders, Rule.Default, "")]
    [TestCase(Family.RequestHeaders, Rule.ExactExcludeOneDestination, "TransactionTrace,ErrorEvent,ErrorTrace,SpanEvent")]
    [TestCase(Family.RequestHeaders, Rule.Wildcard, "TransactionEvent,TransactionTrace,ErrorEvent,ErrorTrace,SpanEvent")]
    public void IncludeExclude_PerDestination(Family family, Rule rule, string expectedDestinations)
    {
        using var rig = new Rig(ConfigFor(family, rule));

        var outputs = rig.Run(Inputs.AllFamilies());

        Assert.That(outputs.Present(family), Is.EqualTo(Normalize(expectedDestinations)));
    }

    [TestCase(Family.RequestParameters, false, "")]
    [TestCase(Family.RequestParameters, true, "TransactionEvent,TransactionTrace,ErrorEvent,ErrorTrace,SpanEvent")]
    [TestCase(Family.RequestHeaders, false, "")]
    [TestCase(Family.RequestHeaders, true, "TransactionEvent,TransactionTrace,ErrorEvent,ErrorTrace,SpanEvent")]
    public void RequestParametersAndHeaders_AreOptIn(Family family, bool included, string expectedDestinations)
    {
        using var rig = new Rig(included ? new Action<configuration>(IncludeRequestAttributes) : null);

        var outputs = rig.Run(Inputs.AllFamilies());

        Assert.That(outputs.Present(family), Is.EqualTo(Normalize(expectedDestinations)));
    }

    [TestCaseSource(nameof(FilterModeCases))]
    public void FilterModes(Family family, FilterMode mode)
    {
        using var rig = new Rig(config =>
        {
            config.browserMonitoring.attributes.enabled = true;
            IncludeRequestAttributes(config);
            ApplyFilterMode(config, mode);
        });
        var removed = DestinationsRemovedBy(mode);
        var expected = removed == null
            ? string.Empty
            : Normalize(string.Join(",", ReachableDestinations(family).Split(',').Except(removed)));

        var outputs = rig.Run(Inputs.AllFamilies());

        Assert.That(outputs.Present(family), Is.EqualTo(expected));
    }

    [TestCase(true)]
    [TestCase(false)]
    public void BrowserDestination_TransactionCustom(bool browserAttributesEnabled)
    {
        using var rig = new Rig(config => config.browserMonitoring.attributes.enabled = browserAttributesEnabled);

        var outputs = rig.Run(Inputs.AllFamilies());

        Assert.Multiple(() =>
        {
            Assert.That(outputs.Present(Family.TransactionCustom).Split(',').Contains(Browser), Is.EqualTo(browserAttributesEnabled));
            Assert.That(Pairs(outputs.User(Browser)).Any(p => p.Key == "foo_tx" && p.Value == "\"1\""), Is.EqualTo(browserAttributesEnabled));
        });
    }

    #endregion

    #region Request headers

    [TestCase(true, "accept,host,x-custom-header")]
    [TestCase(false, "accept,host")]
    public void RequestHeaders_RecordedNames_ForAllowAllHeadersKeyChoice(bool allowAll, string expectedHeaders)
    {
        using var rig = new Rig(config =>
        {
            IncludeRequestAttributes(config);
            config.allowAllHeaders.enabled = allowAll;
        });
        var headers = new Dictionary<string, string> { { "Accept", "text/html" }, { "X-Custom-Header", "abc" }, { "Host", "example.com" } };

        // The key choice mirrors the callers. This test pins only the recorded names for that choice.
        var keys = rig.Configuration.AllowAllRequestHeaders
            ? headers.Keys.ToList()
            : Statics.DefaultCaptureHeaders.Where(headers.ContainsKey).ToList();
        var transaction = rig.NewTransaction();
        transaction.SetRequestHeaders(headers, keys, (d, k) => d[k]);

        var recorded = transaction.TransactionMetadata.UserAndRequestAttributes.GetAttributeValuesDic(AttributeClassification.AgentAttributes).Keys
            .Select(n => n.Substring("request.headers.".Length))
            .OrderBy(n => n, StringComparer.Ordinal);

        Assert.Multiple(() =>
        {
            Assert.That(rig.Configuration.AllowAllRequestHeaders, Is.EqualTo(allowAll));
            Assert.That(string.Join(",", recorded), Is.EqualTo(expectedHeaders));
        });
    }

    [Test]
    public void RequestHeaderCase_IsOneValue()
    {
        using var rig = new Rig(IncludeRequestAttributes);
        var driver = new Driver(rig, Family.RequestHeaders);

        driver.Apply(new[] { Kv("X-Foo", "1"), Kv("x-foo", "2") });

        Assert.Multiple(() =>
        {
            Assert.That(driver.Values.Keys, Is.EqualTo(new[] { "request.headers.x-foo" }));
            Assert.That(driver.Values["request.headers.x-foo"], Is.EqualTo("2"));
            Assert.That(driver.ClassificationCount, Is.EqualTo(1));
        });
    }

    [TestCase(true)]
    [TestCase(false)]
    public void HighSecurityMode(bool highSecurity)
    {
        using var rig = new Rig(config =>
        {
            IncludeRequestAttributes(config);
            config.highSecurity.enabled = highSecurity;
        });

        var outputs = rig.Run(Inputs.AllFamilies());

        Assert.Multiple(() =>
        {
            Assert.That(outputs.AnyNameStartsWith("request.headers."), Is.EqualTo(!highSecurity));
            Assert.That(outputs.AnyNameStartsWith("request.parameters."), Is.EqualTo(!highSecurity));
        });
    }

    #endregion

    #region Names

    [Test]
    public void SameRawName_InTwoFamilies_IsTwoValues()
    {
        using var rig = new Rig(IncludeRequestAttributes);
        var inputs = new Inputs()
            .Add(Family.RequestParameters, "foo", "param")
            .Add(Family.RequestHeaders, "foo", "header");
        inputs.TransactionCustom.Add(Kv("foo", 1));
        inputs.ErrorCustom["foo"] = 2;

        var outputs = rig.Run(inputs);

        Assert.Multiple(() =>
        {
            Assert.That(outputs.Values(TransactionEvent, "request.parameters.foo"), Is.EqualTo(new[] { "\"param\"" }));
            Assert.That(outputs.Values(TransactionEvent, "request.headers.foo"), Is.EqualTo(new[] { "\"header\"" }));
            Assert.That(outputs.User(TransactionEvent), Is.EqualTo("{\"foo\":1}"));
            Assert.That(outputs.User(ErrorEvent), Is.EqualTo("{\"foo\":2,\"foo\":1}"));
            Assert.That(outputs.User(ErrorTrace), Is.EqualTo("{\"foo\":1}"));
            Assert.That(outputs.User(ErrorEventCustomError), Is.EqualTo("{\"foo\":2}"));
        });
    }

    [Test]
    public void CustomName_EqualToStaticName_IsSeparate()
    {
        using var rig = new Rig();
        var inputs = new Inputs().Add(Family.TransactionCustom, "request.uri", "custom");

        var outputs = rig.Run(inputs);

        Assert.Multiple(() =>
        {
            Assert.That(outputs.User(TransactionEvent), Is.EqualTo("{\"request.uri\":\"custom\"}"));
            Assert.That(Pairs(outputs.Agent(TransactionEvent)).Where(p => p.Key == "request.uri").Select(p => p.Value), Is.EqualTo(new[] { "\"/Unknown\"" }));
            Assert.That(outputs.User(TransactionTrace), Is.EqualTo("{\"request.uri\":\"custom\"}"));
            Assert.That(Pairs(outputs.Agent(TransactionTrace)).Where(p => p.Key == "request.uri").Select(p => p.Value), Is.EqualTo(new[] { "\"/Unknown\"" }));
        });
    }

    [Test]
    public void NullName_Throws()
    {
        using var rig = new Rig();
        var segment = rig.NewSegment();
        var transaction = rig.NewTransaction();
        var nullKey = new[] { Kv(null, 1) };
        var nullParameterKey = new[] { new KeyValuePair<string, string>(null, "value") };

        Assert.Multiple(() =>
        {
            Assert.That(() => segment.AddCustomAttribute(null, 1), Throws.TypeOf<ArgumentNullException>());
            Assert.That(() => segment.TryAddEventToSpan("event", StartTime, nullKey), Throws.TypeOf<ArgumentNullException>());
            Assert.That(() => segment.TryAddLinkToSpan("trace", "span", nullKey), Throws.TypeOf<ArgumentNullException>());
            Assert.That(() => rig.RunCustomEvent(nullKey), Throws.TypeOf<ArgumentNullException>());
            Assert.That(() => transaction.AddCustomAttribute(null, 1), Throws.Nothing);
            Assert.That(() => transaction.SetRequestParameters(nullParameterKey), Throws.Nothing);
        });
    }

    #endregion

    #region Serialized output

    private static readonly string[] GoldenDestinations =
    {
        TransactionEvent, TransactionTrace, ErrorEvent, ErrorTrace, SpanEvent, SpanEventSegment, CustomEvent, ErrorEventCustomError, ErrorTraceCustomError
    };

    private static readonly Dictionary<string, string> GoldenJson = new Dictionary<string, string>
    {
        { "TransactionEvent.user", "{\"tx_attr\":\"tx_value\"}" },
        { "TransactionEvent.agent", "{\"host.displayName\":\"test-host\",\"request.headers.x-header\":\"h_value\",\"request.parameters.p_attr\":\"p_value\",\"request.uri\":\"/Unknown\"}" },
        { "TransactionTrace.user", "{\"tx_attr\":\"tx_value\"}" },
        { "TransactionTrace.agent", "{\"request.uri\":\"/Unknown\",\"host.displayName\":\"test-host\",\"request.parameters.p_attr\":\"p_value\",\"request.headers.x-header\":\"h_value\"}" },
        { "ErrorEvent.user", "{\"err_attr\":\"err_value\",\"tx_attr\":\"tx_value\"}" },
        { "ErrorEvent.agent", "{\"host.displayName\":\"test-host\",\"request.headers.x-header\":\"h_value\",\"request.parameters.p_attr\":\"p_value\",\"request.uri\":\"/Unknown\"}" },
        { "ErrorTrace.user", "{\"err_attr\":\"err_value\",\"tx_attr\":\"tx_value\"}" },
        { "ErrorTrace.agent", "{\"request.uri\":\"/Unknown\",\"host.displayName\":\"test-host\",\"request.parameters.p_attr\":\"p_value\",\"request.headers.x-header\":\"h_value\"}" },
        { "SpanEvent.user", "{\"tx_attr\":\"tx_value\"}" },
        { "SpanEvent.agent", "{\"error.class\":\"ErrorType\",\"error.message\":\"error message\",\"host.displayName\":\"test-host\",\"request.headers.x-header\":\"h_value\",\"request.parameters.p_attr\":\"p_value\",\"request.uri\":\"/Unknown\"}" },
        { "SpanEventSegment.user", "{\"span_attr\":\"span_value\"}" },
        { "CustomEvent.user", "{\"event_attr\":\"event_value\"}" },
        { "CustomEvent.agent", "{}" },
        { "ErrorEventCustomError.user", "{\"err_attr\":\"err_value\"}" },
        { "ErrorEventCustomError.agent", "{}" },
        { "ErrorTraceCustomError.user", "{\"err_attr\":\"err_value\"}" },
        { "ErrorTraceCustomError.agent", "{}" },
    };

    [Test]
    public void SerializedOutput_AllSixFamilies()
    {
        using var rig = new Rig(IncludeRequestAttributes);
        var inputs = new Inputs()
            .Add(Family.TransactionCustom, "tx_attr", "tx_value")
            .Add(Family.SpanCustom, "span_attr", "span_value")
            .Add(Family.ErrorCustom, "err_attr", "err_value")
            .Add(Family.CustomEvent, "event_attr", "event_value")
            .Add(Family.RequestParameters, "p_attr", "p_value")
            .Add(Family.RequestHeaders, "X-Header", "h_value");

        var outputs = rig.Run(inputs);

        Assert.Multiple(() =>
        {
            foreach (var destination in GoldenDestinations)
            {
                Assert.That(outputs.User(destination), Is.EqualTo(GoldenJson[destination + ".user"]), destination + " user");

                // The span's agent attributes depend on the static code level metrics setting, so they are not pinned.
                if (destination != SpanEventSegment)
                {
                    Assert.That(outputs.Agent(destination), Is.EqualTo(GoldenJson[destination + ".agent"]), destination + " agent");
                }
            }
        });
    }

    [Test]
    public void SpanEventAndLink_KeepNames()
    {
        using var rig = new Rig();
        var transaction = rig.NewTransaction();
        var segment = rig.NewSegment();
        segment.TryAddEventToSpan("event", StartTime, new[] { Kv("foo", "1") });
        segment.TryAddLinkToSpan("trace", "span", new[] { Kv("foo", "1") });
        transaction.Add(segment);
        var immutable = rig.Immutable(transaction);

        var spans = rig.MakeSpans(immutable, rig.MakeAttributes(immutable));
        var span = SpanObjects(spans[1]);
        var link = SpanObjects(spans[1], 1);
        var spanEvent = SpanObjects(spans[1], 2);

        Assert.Multiple(() =>
        {
            Assert.That(span[0], Is.EqualTo("{}"));
            Assert.That(link[0], Is.EqualTo("{\"foo\":\"1\"}"));
            Assert.That(spanEvent[0], Is.EqualTo("{\"foo\":\"1\"}"));
        });
    }

    #endregion

    #region Configuration change

    [Test]
    public void ConfigurationChange_NewTransactionUsesNewRules()
    {
        using var rig = new Rig(IncludeRequestAttributes);
        var transactionA = rig.NewTransaction();
        transactionA.AddCustomAttribute("foo", "a");
        transactionA.SetRequestParameters(new[] { new KeyValuePair<string, string>("foo", "a") });

        rig.Reconfigure(config =>
        {
            config.attributes.exclude = new List<string> { "foo", "baz" };
            config.attributes.include = new List<string>();
        });

        var transactionB = rig.NewTransaction();
        transactionB.AddCustomAttribute("foo", "b");
        transactionB.AddCustomAttribute("baz", "b");
        transactionB.AddCustomAttribute("bar", "b");
        transactionB.SetRequestParameters(new[] { new KeyValuePair<string, string>("foo", "b") });

        var outputA = rig.TransactionEventObjects(transactionA);
        var outputB = rig.TransactionEventObjects(transactionB);

        Assert.Multiple(() =>
        {
            Assert.That(outputA[0], Is.EqualTo("{\"foo\":\"a\"}"));
            Assert.That(Pairs(outputA[1]).Select(p => p.Key), Does.Contain("request.parameters.foo"));
            Assert.That(outputB[0], Is.EqualTo("{\"bar\":\"b\"}"));
            Assert.That(Pairs(outputB[1]).Select(p => p.Key), Does.Not.Contain("request.parameters.foo"));
        });
    }

    #endregion
}
