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
$env:CORECLR_NEWRELIC_HOME    = "$home_dir"
$env:NEWRELIC_LICENSE_KEY     = "<staging-key>"
$env:NEWRELIC_APP_NAME        = "SignalR-POC-Server"
$env:NEWRELIC_LOG_LEVEL       = "debug"
# Hybrid OTel bridge — required so the SignalR ActivitySource is consumed.
$env:NEW_RELIC_OPENTELEMETRY_TRACING_ENABLED = "true"

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
$env:CORECLR_NEWRELIC_HOME    = "$home_dir"
$env:NEWRELIC_LICENSE_KEY     = "<staging-key>"
$env:NEWRELIC_APP_NAME        = "SignalR-POC-Client"
$env:NEWRELIC_LOG_LEVEL       = "debug"
$env:NEW_RELIC_OPENTELEMETRY_TRACING_ENABLED = "true"

dotnet run --project poc/SignalR/Client/SignalRPocClient.csproj -- http://localhost:5050/chathub 3
```

The client takes two optional positional args: `<hubUrl> <iterations>`.

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
