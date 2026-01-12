// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using Newtonsoft.Json;

namespace CompositeTests.HybridAgent.Helpers;

public class HybridAgentTestCase
{
    [JsonProperty("testDescription")]
    public string TestDescription { get; set; }

    [JsonProperty("operations")]
    public IEnumerable<Operation> Operations { get; set; }

    [JsonProperty("agentOutput")]
    public AgentOutput Telemetry { get; set; }
}

public class Operation
{
    [JsonProperty("command")]
    public string Command { get; set; }

    [JsonProperty("parameters")]
    public IDictionary<string, object> Parameters { get; set; }

    [JsonProperty("childOperations")]
    public IEnumerable<Operation> ChildOperations { get; set; }

    [JsonProperty("assertions")]
    public IEnumerable<Assertion> Assertions { get; set; }
}

public class Assertion
{
    [JsonProperty("description")]
    public string Description { get; set; }

    [JsonProperty("rule")]
    public AssertionRule Rule { get; set; }
}

public class AssertionRule
{
    [JsonProperty("operator")]
    public string Operator { get; set; }

    [JsonProperty("parameters")]
    public IDictionary<string, object> Parameters { get; set; }
}

public class AgentOutput
{
    [JsonProperty("transactions")]
    public IEnumerable<Transaction> Transactions { get; set; }

    [JsonProperty("spans")]
    public IEnumerable<Span> Spans { get; set; }
}

public class Transaction
{
    [JsonProperty("name")]
    public string Name { get; set; }

    [JsonProperty("attributes")]
    public IDictionary<string, object> Attributes { get; set; }
}

public class Span
{
    [JsonProperty("name")]
    public string Name { get; set; }

    [JsonProperty("category")]
    public string Category { get; set; }

    [JsonProperty("parentName")]
    public string ParentName { get; set; }

    [JsonProperty("entryPoint")]
    public bool? EntryPoint { get; set; }

    [JsonProperty("attributes")]
    public IDictionary<string, object> Attributes { get; set; }

    [JsonProperty("links")]
    public IEnumerable<SpanLink> Links { get; set; }

    [JsonProperty("events")]
    public IEnumerable<SpanEvent> Events { get; set; }
}

public class SpanLink
{
    [JsonProperty("linkedTraceId")]
    public string LinkedTraceId { get; set; }

    [JsonProperty("linkedSpanId")]
    public string LinkedSpanId { get; set; }

    [JsonProperty("attributes")]
    public IDictionary<string, object> Attributes { get; set; }
}

public class SpanEvent
{
    [JsonProperty("name")]
    public string Name { get; set; }

    [JsonProperty("attributes")]
    public IDictionary<string, object> Attributes { get; set; }
}
