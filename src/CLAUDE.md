# Source Code Architecture

Orientation map for `src/`. See the [root claude.md](../CLAUDE.md) for how
instrumentation works end-to-end and for coding standards.

## Layout

```
src/Agent/
├── NewRelic/
│   ├── Agent/
│   │   ├── Core/        # Managed agent core (C#)
│   │   └── Extensions/  # Instrumentation framework + per-framework wrappers
│   ├── Profiler/        # Native profiler (C++)
│   └── Home/            # Home-directory layout project (assembles newrelichome_*)
├── NewRelic.Api.Agent/  # Public API consumed by customer code
├── Configuration/       # XSD / config surface (see note below)
├── MsiInstaller/        # Windows MSI
├── newrelichome_*/      # Built agent homes (FullAgent.sln output)
└── Scripts/             # Misc maintenance scripts (e.g. flush_dotnet_temp.cmd)
```

## Profiler (`Agent/NewRelic/Profiler/`)

Native C++ implementing the CLR Profiling API. On JIT, matches methods
against instrumentation XML, requests ReJIT, and wraps the bytecode with
`try/catch/finally` IL that calls into `AgentShim`.

**Entry points worth knowing:**
- `Profiler/CorProfilerCallbackImpl.h` — main profiler callbacks
- `MethodRewriter/InstrumentFunctionManipulator.h` — bytecode injection
- `MethodRewriter/FunctionManipulator.h` — low-level IL manipulation

**Build:** `src/Agent/NewRelic/Profiler/build/build.ps1`
```powershell
build.ps1 -Platform x64 -Configuration Debug    # Windows x64
build.ps1 -Platform linux                       # Linux (requires Docker)
```

Profiler GUIDs are documented in the [root claude.md](../CLAUDE.md).

**Injected CoreLib helper methods must stay "tiny" (hard, unguarded
constraint).** The cache getter/store helpers the profiler injects into
CoreLib/mscorlib (`MethodRewriter/HelperFunctionManipulator.h`, the `Build*`
methods) are emitted via `HelperFunctionManipulator::InstrumentHelper` ->
`FunctionManipulator::InstrumentTiny`, which ALWAYS writes an ECMA-335 tiny
method header. This path is tiny-only: no size check, no fat-header fallback
(the fat-upgrade path `FunctionManipulator::ExtractHeaderBodyAndExtra` exists
but is not on the helper call path). A tiny method is limited to: IL code
size < 64 bytes (6-bit field), maxstack <= 8 (fixed, never written), no
locals, no exception-handling clauses (see
`externals/coreclr-headers/src/inc/corhdr.h` and `corhlpr.h`). Exceeding any
limit does NOT fail loudly: `InstrumentTiny` sets
`Flags_CodeSize = (uint8_t)((codeSize << 2) | 0x2)` and silently truncates to
one byte, so a >= 64-byte body encodes a wrong (smaller) size and the CLR
then reads a malformed method (InvalidProgramException / JIT failure /
crash). Nothing asserts or tests this (the tiny/fat boundary tests in
`MethodRewriterTest/FunctionManipulatorTest.cpp` are disabled) -- it is
enforced by convention only. Rule: keep every injected helper minimal; when
adding IL to a `Build*` helper, budget the bytes (single-byte opcodes 1 B;
short-form branch 2 B; `call`/`callvirt`/`ldsfld`/`stsfld`/`castclass`/
`ldstr`/`newobj` with a 4-byte token 5 B), keep live stack <= 8, add no
locals. Verify empirically in a debug build by logging
`_instructions->GetBytes().size()` before `InstrumentTiny()`; do not trust it
to fail safely.

## Agent Core (`Agent/NewRelic/Agent/Core/`)

```
Core/
├── AgentHealth/          Aggregators/         Api/
├── Attributes/           BrowserMonitoring/   CallStack/
├── Commands/             Configuration/       DataTransport/
├── DependencyInjection/  DistributedTracing/  Errors/
├── Events/               Instrumentation/     Metrics/
├── Samplers/             Segments/            Spans/
├── ThreadProfiling/      Transactions/        TransactionTraces/
├── Transformers/         Utilities/           Utilization/
├── WireModels/           Wrapper/
```

**Notable classes / entry points:**
- **Transactions:** `Transaction`, `ImmutableTransaction` (snapshot handed
  to the pipeline when the transaction ends), `TransactionName`,
  `TransactionMetricNameMaker`. Lifecycle: created by framework
  instrumentation → segments added as operations run → custom attributes
  collected → finished → transformed → aggregated into metrics, traces,
  events, and spans.
- **Segments:** `Segment` plus `*SegmentData` variants that carry the
  per-category metadata. Segment categories: **external** (HTTP client
  calls), **datastore** (SQL, NoSQL, caches), **message broker**
  (queue/topic produce & consume), and **custom** (public-API or
  wrapper-defined). Wrappers pick the category by which `*SegmentData`
  they attach.
- **Distributed tracing:** `DistributedTracePayload`,
  `DistributedTracingApiModel`, `TracePriorityManager`. Inbound wrappers
  accept trace context from headers and link the current transaction to
  its parent span; outbound wrappers inject trace context into outgoing
  calls. Implements W3C Trace Context plus the New Relic payload.
- **Data transport:** `ConnectionManager`, `DataTransportService`,
  `HttpCollectorWire`, `AgentCommands` (processes server-side config
  returned by the collector).
- **Aggregators:** one per data type (metrics, transaction events, error
  events, span events, custom events, SQL traces, transaction traces).
  Harvest cycle is ~60s by default: aggregators sample/combine, serialize,
  and ship, then reset.
- **DI:** `Core/DependencyInjection/AgentContainer`.

**Data pipeline:**
```
method → tracer/wrapper → segment/transaction → ImmutableTransaction
      → transformers → aggregators → DataTransport → collector
```

## Extensions (`Agent/NewRelic/Agent/Extensions/`)

```
Extensions/NewRelic.Agent.Extensions/
└── Providers/
    ├── Storage/   Async-context storage (AsyncLocal, CallContext, HttpContext,
    │             OperationContext, HybridHttpContext)
    └── Wrapper/   Per-framework instrumentation wrappers (40+ projects —
                   one subfolder per instrumented library; ls to enumerate)
```

### Creating a wrapper

Each wrapper project contains the csproj, an `*Instrumentation.xml` that
tells the profiler what to hook, and the `IWrapper` implementation(s).

Minimal instrumentation XML:
```xml
<extension>
  <instrumentation>
    <tracerFactory name="MyTracerFactory">
      <match assemblyName="MyFramework" className="MyClass">
        <exactMethodMatcher methodName="MyMethod" />
      </match>
    </tracerFactory>
  </instrumentation>
</extension>
```

Minimal wrapper:
```csharp
public class MyFrameworkWrapper : IWrapper
{
    // true  = wrapper is skipped if no transaction is in progress
    //         (use when the wrapper only adds a segment to an existing tx)
    // false = wrapper runs regardless; typically it creates a transaction
    //         itself (entry points: request handlers, message consumers,
    //         lambda handlers, background job starts)
    public bool IsTransactionRequired => true;

    public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo) =>
        new CanWrapResponse(methodInfo.RequestedWrapperName == nameof(MyFrameworkWrapper));

    public AfterWrappedMethodDelegate BeforeWrappedMethod(
        InstrumentedMethodCall instrumentedMethodCall,
        IAgent agent,
        ITransaction transaction)
    {
        var segment = transaction.StartTransactionSegment(
            instrumentedMethodCall.MethodCall, "MyFramework");
        return Delegates.GetDelegateFor(segment);
    }
}
```

**Debugging tip:** if a wrapper seems not to fire, check
`IsTransactionRequired`. A `true` wrapper on a method that runs outside
any transaction (e.g. a background poller, a message consumer invoked by
an SDK thread before a transaction exists) will be silently skipped —
`transaction` would be null, so the agent bypasses the wrapper entirely.
Wrappers that *start* transactions must return `false`.

`maxVersion` in instrumentation XML is **exclusive**. Prefer
`VisibilityBypasser` over reflection / `dynamic` when reaching into
instrumented types; cache generated delegates per type. Both conventions
are detailed in the [root claude.md](../CLAUDE.md).

Wrapper projects have **no unit tests** — they're covered by integration
and unbounded test solutions. Only `NewRelic.Agent.Extensions` (shared
helpers like `SqsHelper`) is unit tested. When adding non-trivial logic
to a wrapper, lift it into a helper in `NewRelic.Agent.Extensions` so it
can be unit tested — keep the wrapper itself thin. The same rule is
covered in the [root claude.md](../CLAUDE.md) testing conventions.

## Public API (`NewRelic.Api.Agent/`)

Customer-facing surface:
- Interfaces: `IAgent`, `ITransaction`, `ISegment`, `ISpan`
- Static helpers on `NewRelic.Api.Agent.NewRelic` (`AddCustomAttribute`,
  `NoticeError`, `SetTransactionName`, `GetAgent`, `StartAgent`)
- Attributes: `[Transaction]`, `[Trace]` for custom instrumentation

Changes here are gated by `PublicApiChangeTests` — intentional breaks need
an explicit baseline update.

## Configuration

Runtime config precedence is in the [root claude.md](../CLAUDE.md).

The canonical schema is `src/Agent/NewRelic/Agent/Core/Config/Configuration.xsd`
and it generates `Configuration.cs` in the same directory.
`src/Agent/Configuration/` holds additional config surface / validation
artifacts.

### Regenerating `Configuration.cs` after editing the XSD

`Configuration.cs` is **auto-generated — never hand-edit it.** After
changing `Configuration.xsd`, regenerate by running the following from
the repo root:

```powershell
$root = (Resolve-Path .).Path
& "$root\build\Tools\xsd2code\xsd2code.exe" `
  "$root\src\Agent\NewRelic\Agent\Core\Config\Configuration.xsd" `
  NewRelic.Agent.Core.Config `
  "$root\src\Agent\NewRelic\Agent\Core\Config\Configuration.cs" `
  /cl /ap /sc /xa
```

`xsd2code` strips file headers, so re-prepend the two-line license header
to the regenerated file:
```
// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0
```

See [docs/config-development.md](../docs/config-development.md) for the
full reference on config development.

## Performance principles

The agent runs inside every instrumented process, so overhead directly
taxes customer apps. When writing or modifying agent code, honor these:

- **Minimize allocations on hot paths.** Tracer start/finish, segment
  creation, and per-call wrapper code run on every instrumented method
  call. Avoid LINQ, closures, string concatenation, `params` arrays, and
  boxing in these paths.
- **Cache reflection.** Any `Type.GetMethod` / `PropertyInfo` / generated
  delegate must be cached per type (typically in a
  `ConcurrentDictionary<Type, …>`). In wrapper projects, use
  `VisibilityBypasser` — it both caches and compiles to IL (see root
  claude.md).
- **Keep the non-instrumented fast path cheap.** When a method isn't
  matched by any wrapper, the agent code in its path should do as little
  as possible — ideally a single guard check.
- **Lazy initialization** for anything not needed at agent startup.
- **Favor immutable + snapshot handoffs** (see `ImmutableTransaction`)
  over locking shared mutable state; use lock-free structures where
  practical and `ReaderWriterLockSlim` when you must share.
- **Prefer pooling / reuse** (`StringBuilder`, buffers, event reservoirs)
  on repeated per-transaction work.

**Sampling keeps volume bounded:**
- Transaction traces: 1/min by default.
- Span events: sampled by priority.
- Custom events: reservoir sampling.

Respect these when adding new telemetry — unsampled firehoses are not an
option.

## Agent initialization order

1. CLR loads profiler via `*_PROFILER_PATH`.
2. Profiler reads instrumentation XML from the agent home + extensions
   directory and sets event masks.
3. Agent core `AgentInitializer.Initialize()` runs: loads config from all
   sources, registers services in `AgentContainer`, opens connection to
   New Relic, starts samplers and aggregators.
4. JIT compiles application methods; matched methods get ReJIT'd with
   instrumented bytecode.
5. Harvest cycle (~60s) aggregates and ships data.

## Debugging signals

When an agent appears attached but does nothing:
- Check the agent log (`<home>/logs/`) for a **"Profiler attached"** line —
  absence means the profiler never loaded.
- If the log itself is missing, the profiler failed to load. Check the
  Windows **Event Viewer** (Applications and Services Logs → Application)
  for profiler DLL load errors — GUID mismatch, bitness mismatch, or
  missing dependencies all surface here.
- Turn up verbosity with `NEWRELIC_LOG_LEVEL=debug` for the managed side.
