# SignalR Instrumentation POC (NR-232592)

POC for adding SignalR coverage to the .NET agent via the existing OpenTelemetry
hybrid bridge. See the spike for context:

- Summary: https://newrelic.atlassian.net/wiki/spaces/APM/pages/5605458057
- Detailed Findings: https://newrelic.atlassian.net/wiki/spaces/APM/pages/5605130389

## What this POC contains

| Path | Purpose |
| --- | --- |
| `Server/` | ASP.NET Core 10 SignalR sample with one `ChatHub` exposing regular invoke, error, streaming, and `OnConnected`/`OnDisconnected` overrides |
| `Client/` | .NET 10 console app driving the hub to exercise acceptance criteria 1–6 |

Plus the only production-code change required for the POC:
**`src/Agent/NewRelic/Agent/Core/Configuration/DefaultConfiguration.cs`** —
`"Microsoft.AspNetCore.SignalR.Server"` removed from
`OpenTelemetryTracingExcludedActivitySources`. Without this change the bridge
silently drops every SignalR activity. The matching `Microsoft.AspNetCore.SignalR.Client`
source is already absent from the exclusion list.

The sample app does **not** reference the OpenTelemetry SDK packages. The
agent's `ActivityBridge` (`src/Agent/NewRelic/Agent/Core/OpenTelemetryBridge/Tracing/ActivityBridge.cs`)
registers its own `ActivityListener`; subscribing the customer app to OTel is
not required.

### Why the client uses `[Transaction]`

The bridge only starts a New Relic transaction for activities that are
`Server`/`Consumer`-kind or have a remote parent. The client-side
`Microsoft.AspNetCore.SignalR.Client` `InvocationOut` activity is `Client`-kind
without a parent, so the bridge will only attach it as a *segment* — and only
when a transaction is already in progress. A console app is not auto-instrumented
by the agent, so without an explicit transaction boundary nothing anchors the
activity, no NR trace context bridges onto it, and DT to the server breaks.

The client therefore takes a `PackageReference` on `NewRelic.Agent.Api` and
decorates each per-invocation method with `[Transaction]`. The agent's profiler wraps those method bodies in a
"Background" transaction whose trace context is propagated onto the SignalR
client activity, which in turn injects W3C `traceparent` into the hub
invocation message headers.

## Prerequisites

- .NET 10 SDK
- A built FullAgent.sln so `src/Agent/newrelichome_x64_coreclr` exists with the
  agent home contents (see project `CLAUDE.md`)
- A New Relic staging license key

## Build

```powershell
dotnet build poc/SignalR/Server/SignalRPocServer.csproj
dotnet build poc/SignalR/Client/SignalRPocClient.csproj
```

## Run

Server (env vars wire up the .NET Core agent — paths reference the agent home
the FullAgent.sln build emits):

```powershell
$home_dir = "$((Resolve-Path .).Path)\src\Agent\newrelichome_x64_coreclr"
$env:CORECLR_ENABLE_PROFILING = "1"
$env:CORECLR_PROFILER         = "{36032161-FFC0-4B61-B559-F6C5D41BAE5A}"
$env:CORECLR_PROFILER_PATH    = "$home_dir\NewRelic.Profiler.dll"
$env:CORECLR_NEW_RELIC_HOME    = "$home_dir"
$env:NEW_RELIC_LICENSE_KEY     = "<staging-key>"
$env:NEW_RELIC_APP_NAME        = "SignalR-POC-Server"
$env:NEW_RELIC_LOG_LEVEL       = "debug"
# Hybrid OTel bridge — required so the SignalR ActivitySource is consumed.
# Master switch AND per-signal toggles. Tracing wires the ActivityListener;
# metrics is needed for acceptance criterion 5 (HttpConnections meter).
$env:NEW_RELIC_OPENTELEMETRY_ENABLED         = "true"
$env:NEW_RELIC_OPENTELEMETRY_TRACES_ENABLED  = "true"
$env:NEW_RELIC_OPENTELEMETRY_METRICS_ENABLED = "true"

dotnet run --project poc/SignalR/Server/SignalRPocServer.csproj
```

Client (separate shell — also attach the agent so the client-side
`Microsoft.AspNetCore.SignalR.Client` `ActivitySource` injects W3C
trace context into hub invocation messages):

```powershell
$home_dir = "$((Resolve-Path .).Path)\src\Agent\newrelichome_x64_coreclr"
$env:CORECLR_ENABLE_PROFILING = "1"
$env:CORECLR_PROFILER         = "{36032161-FFC0-4B61-B559-F6C5D41BAE5A}"
$env:CORECLR_PROFILER_PATH    = "$home_dir\NewRelic.Profiler.dll"
$env:CORECLR_NEW_RELIC_HOME    = "$home_dir"
$env:NEW_RELIC_LICENSE_KEY     = "<staging-key>"
$env:NEW_RELIC_APP_NAME        = "SignalR-POC-Client"
$env:NEW_RELIC_LOG_LEVEL       = "debug"
$env:NEW_RELIC_OPENTELEMETRY_ENABLED         = "true"
$env:NEW_RELIC_OPENTELEMETRY_TRACES_ENABLED  = "true"
$env:NEW_RELIC_OPENTELEMETRY_METRICS_ENABLED = "true"

dotnet run --project poc/SignalR/Client/SignalRPocClient.csproj -- http://localhost:5050/chathub 3
```

The client takes two optional positional args: `<hubUrl> <iterations>`.

### Burst / autocomplete-shape load test

To characterize how the bridge behaves under an autocomplete-shaped workload
(many short hub invocations from many concurrent connections), the client
also has a `--burst` mode that drives the `Search(prefix)` hub method:

```powershell
# --burst <hubUrl> <connections> <durationSec> [keystrokeMs] [searchTerm]
dotnet run --project poc/SignalR/Client/SignalRPocClient.csproj -- `
    --burst http://localhost:5050/chathub 50 30 80 alpha
```

The above opens 50 concurrent `HubConnection`s and "types" the term `alpha`
one character at a time on each connection (one `Search` invoke every 80 ms),
for 30 seconds. The client prints throughput and p50/p95/p99 latency at the
end. Run it three ways and compare:

1. **Baseline** — server started with no agent attached.
2. **Agent attached, SignalR source on the exclusion list** — set
   `NEW_RELIC_OPENTELEMETRY_TRACES_EXCLUDE=Microsoft.AspNetCore.SignalR.Server`
   to confirm the bridge add cost is the dominant overhead and not the agent
   itself.
3. **Agent attached, SignalR source bridged** (the POC's default).

Then check NRDB for transaction-name cardinality and volume:

```sql
FROM Transaction
SELECT count(*), uniqueCount(name), percentile(duration, 50, 95, 99)
WHERE appName = 'SignalR-POC-Server'
FACET name
SINCE 5 minutes ago
```

Ideally `uniqueCount(name)` stays at 1 per hub method (no leakage of the
search prefix into the transaction name), and the transaction count matches
the client-side invocation count.

## Acceptance criteria → how to verify

| # | Criterion | NRQL / verification |
| --- | --- | --- |
| 1 | Hub method invocation appears as a transaction | `FROM Transaction SELECT * WHERE appName = 'SignalR-POC-Server' SINCE 30 minutes ago` — expect rows whose `name` resolves to the `ChatHub.SendMessage` invocation |
| 2 | Errors thrown from a hub method appear on the transaction | `FROM TransactionError SELECT * WHERE appName = 'SignalR-POC-Server'` — expect `InvalidOperationException` from `ThrowSomething` |
| 3 | Single connected distributed trace from client → hub method | `FROM Span SELECT * WHERE trace.id = '<trace id from a client invocation>'` — expect both client and server spans on one trace |
| 4 | Streaming method's transaction spans the producing enumerable's lifetime | `FROM Transaction SELECT duration WHERE appName = 'SignalR-POC-Server' AND name LIKE '%Counter%'` — duration ≥ `count * delayMs` (≈ 500 ms by default) |
| 5 | Connection metrics from `Microsoft.AspNetCore.Http.Connections` land in NRDB | `FROM Metric SELECT * WHERE metricName LIKE 'signalr.server.%' SINCE 30 minutes ago` |
| 6 | `OnConnectedAsync` / `OnDisconnectedAsync` activities visible (transaction or span) | Inspect a trace from above and look for `*OnConnected` / `*OnDisconnected` activity names |

A pass on 1–4 is the minimum bar. Gaps on 5–6 are documented findings, not POC
failures (per the spike's POC plan).

## Likely bridge-side findings to watch for

These are the items the spike doc flags as POC decisions; record observations
in NR-232592 as comments.

1. **Transaction naming.** Default will be
   `WebTransaction/Microsoft.AspNetCore.SignalR.Server/<HubFullName>/<Method>`.
   If we want `WebTransaction/SignalR/<Hub>/<Method>` instead, that's an
   `ActivityBridgeHelpers.StartTransactionForActivity` mapping change.
2. **`rpc.system` vs `rpc.system.name`.** OTel RPC semconv is migrating; do
   not lock the bridge to a single tag spelling.
3. **`OnConnectedAsync`/`OnDisconnectedAsync` are `ActivityKind.Internal`** so
   they will not start a transaction by themselves. Whether to keep them as
   nested spans or promote them is a v1 product decision.
4. **Blazor Server overlap.** `Microsoft.AspNetCore.Components.Server.Circuits`
   is still in the exclusion list (`DefaultConfiguration.cs:2837`). Out of
   scope for this POC; revisit during MVP per
   [aspnetcore #62254](https://github.com/dotnet/aspnetcore/issues/62254).
