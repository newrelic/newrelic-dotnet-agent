# New Relic .NET Agent

Profiler-based APM agent for .NET Framework and .NET Core on Windows and Linux.
A native C++ profiler injects IL into JIT-compiled methods; the managed agent
core collects telemetry and ships it to the collector.

Sub-docs (read when working in that area):
- [src/CLAUDE.md](src/CLAUDE.md) — agent/profiler/extensions internals
- [build/CLAUDE.md](build/CLAUDE.md) — build, packaging, release
- [tests/CLAUDE.md](tests/CLAUDE.md) — unit + integration test layout

## Solutions

- **FullAgent.sln** (repo root) — primary. Builds managed code; emits
  platform-specific `src/Agent/newrelichome_*` directories. Use for almost
  all work.
- **Profiler.sln** (`src/Agent/NewRelic/Profiler/`) — native C++ profiler.
  Only needed when changing the profiler itself; otherwise consumed via NuGet.
- **IntegrationTests.sln** (`tests/Agent/IntegrationTests/`) — host-run
  end-to-end tests against real test applications. Requires a built
  FullAgent.sln (agent home directories must exist).
- **UnboundedIntegrationTests.sln** (`tests/Agent/IntegrationTests/`) —
  integration tests that require external infrastructure (databases,
  brokers, etc.). Start services first via
  `tests/Agent/IntegrationTests/UnboundedServices` docker compose.
- **ContainerIntegrationTests.sln** (`tests/Agent/IntegrationTests/`) —
  tests that run instrumented apps inside Docker containers; primary
  coverage for the Linux agent and container-specific scenarios. Needs
  Docker Desktop.

## Agent home directories

Building `FullAgent.sln` creates/updates these directories under `src/Agent/`.
Integration, unbounded, and container tests all read from them — so before
running any of those test solutions, (re)build `FullAgent.sln` first so the
test runs pick up your latest changes.

| Framework | OS    | Arch  | Directory                         |
|-----------|-------|-------|-----------------------------------|
| .NET FW   | Win   | x64   | `newrelichome_x64`                |
| .NET FW   | Win   | x86   | `newrelichome_x86`                |
| .NET Core | Win   | x64   | `newrelichome_x64_coreclr`        |
| .NET Core | Win   | x86   | `newrelichome_x86_coreclr`        |
| .NET Core | Linux | x64   | `newrelichome_x64_coreclr_linux`  |
| .NET Core | Linux | arm64 | `newrelichome_arm64_coreclr_linux`|

## Attaching the agent locally

.NET Framework:
```
COR_ENABLE_PROFILING=1
COR_PROFILER={71DA0A04-7777-4EC6-9643-7D28B46A8A41}
COR_PROFILER_PATH=<home>\NewRelic.Profiler.dll
NEWRELIC_HOME=<home>
NEWRELIC_LICENSE_KEY=...
```

.NET Core/.NET:
```
CORECLR_ENABLE_PROFILING=1
CORECLR_PROFILER={36032161-FFC0-4B61-B559-F6C5D41BAE5A}
CORECLR_PROFILER_PATH=<home>\NewRelic.Profiler.dll
CORECLR_NEWRELIC_HOME=<home>
NEWRELIC_LICENSE_KEY=...
```

Debug logs: `NEWRELIC_LOG_LEVEL=debug` → `<home>/logs/`. Profiler load
failures surface in the Windows Event Viewer.

## How instrumentation works (short version)

1. CLR loads the profiler via `*_PROFILER_PATH`.
2. Profiler subscribes to JIT events and consults XML extension files under
   `src/Agent/NewRelic/Agent/Extensions/Providers/Wrapper/*` to decide which
   methods to rewrite.
3. Target methods get wrapped in try/catch/finally IL that calls
   `AgentShim.GetFinishTracerDelegate()` to start/finish a tracer.
4. Tracers record timing, errors, and segment data into the current
   transaction.

**Runtime extension layout:** at runtime, instrumentation XML and wrapper
DLLs live in `<agent-home>/extensions/` (and `extensions/netcore/` for
the .NET Core wrappers). The agent watches this directory and picks up
new or modified XML without a process restart — if new instrumentation
isn't taking effect, verify the files are actually present there.

### instrumentation.xml version ranges

`maxVersion` is **exclusive** (strictly less than). To cover all versions up
to but not including 9.7.0, write `maxVersion="9.7.0"` — not `"9.6.9999"`.

## Configuration

Precedence: env vars > `newrelic.config` > server-side config > defaults.

- Models: `src/Agent/NewRelic/Agent/Core/Configuration/`
- Loader: `src/Agent/NewRelic/Agent/Core/Config/`

**Editing `Configuration.xsd`** requires regenerating `Configuration.cs`
via `xsd2code` and restoring the license header — never hand-edit the
generated file. Exact command and caveats in
[src/CLAUDE.md](src/CLAUDE.md) under Configuration.

## Building and testing from the CLI

Full build: open `FullAgent.sln` in the latest Visual Studio or run its
MSBuild equivalent.

**Core.UnitTest** (and any project depending on `Core.csproj`) fails from
the CLI with `AssemblyModifier.exe` / `*Undefined*` errors unless
`SolutionDir` is passed explicitly — VS sets it, plain `dotnet` does not:

Run from the repo root so `$PWD` resolves correctly; the trailing backslash
on `SolutionDir` is required by MSBuild:

```powershell
$sln = "$((Resolve-Path .).Path)\"
dotnet build tests/Agent/UnitTests/Core.UnitTest/Core.UnitTest.csproj `
  -f net10.0 -p:SolutionDir="$sln"
dotnet test  tests/Agent/UnitTests/Core.UnitTest/Core.UnitTest.csproj `
  -f net10.0 -p:SolutionDir="$sln" `
  --filter "FullyQualifiedName~SomeTest"
```

Root cause: `Core.csproj` has a post-build ILRepack + AssemblyModifier step
that references `$(SolutionDir)`.

**Extensions tests** (`NewRelic.Agent.Extensions.Tests`) build fine with
plain `dotnet build`, but `dotnet test` against the `.csproj` silently fails
via VSTestTask. Run against the built DLL instead:

```
dotnet test tests/Agent/UnitTests/NewRelic.Agent.Extensions.Tests/bin/Debug/net10.0/NewRelic.Agent.Extensions.Tests.dll `
  --filter "FullyQualifiedName~SomeTest"
```

**Unbounded integration tests** need infrastructure containers first:

```
cd tests/Agent/IntegrationTests/UnboundedServices
docker compose up -d
```

## Analyzing agent and integration-test logs

Agent logs and integration-test `.testlog` files are huge (hundreds of KB) with
single lines tens of KB wide -- the `connect`/`agent_settings` payloads and the
`span_event_data` / `transaction_sample_data` / `metric_data` /
`analytic_event_data` harvests are base64 blobs and full JSON. Never raw-read,
`tail`, or wide-grep them; the bytes land in context and get re-billed every
later turn. Instead:

- Strip the payload noise into a slim file first, then work from it:
  `grep -avE 'span_event_data|transaction_sample_data|metric_data|analytic_event_data' big.testlog > /tmp/slim.log`
- Counts before content: `grep -c`, `grep -o` (matched token only),
  `sort | uniq -c`. Much is learnable without a full line -- skip-line count,
  wrapper-selection counts, `TraceContext/Accept/Success` presence.
- Cap line width on anything that may print a payload: pipe through `cut -c1-200`.
- Mind log level before concluding a line's absence means the event did not
  happen. The sender/SUT app is often at `debug` while the receiver is at
  `all`/finest, so finest-only lines ("Skipping HttpWebRequest header injection",
  `Segment start`) show for one app but not the other. Check `Log level set to`
  per pid first.
- When you only need a conclusion, hand the whole log to a subagent with a
  specific question rather than reading it in the main thread.

## Testing conventions

- **New/changed code in `Core` and `NewRelic.Agent.Extensions` must ship with
  unit tests in the same change (unprompted), targeting 100% reachable
  coverage.** Enforced by the `dotnet-unit-test-coverage` skill
  (`.claude/skills/`), which also tells plan-writing to bake this in. Wrapper
  projects are exempt (see below).
- Unit tests: `tests/Agent/UnitTests/` (NUnit primary, xUnit used in some
  places). Mocking is **JustMock Lite** (free tier) — interfaces and virtual
  members only. No sealed / static / non-virtual mocking. Design new code
  with interfaces and virtual methods so it can be mocked.
- **Wrapper projects** under `src/.../Extensions/Providers/Wrapper/*` have
  **no unit tests and should not** — they are covered by the Integration
  and Unbounded test solutions only. The `NewRelic.Agent.Extensions` project
  (shared helpers like `SqsHelper`) is the only Extensions-side code that
  is unit tested. When adding non-trivial logic to a wrapper — parsing,
  URI/version handling, header manipulation, data-shape transforms — lift
  it into a helper in `NewRelic.Agent.Extensions` and call it from the
  wrapper. That keeps the interesting logic unit-testable while the
  wrapper itself stays thin (match, create segment, delegate, finish).
- Integration tests: `tests/Agent/IntegrationTests/` — see
  [tests/CLAUDE.md](tests/CLAUDE.md).
- **Never use `InternalsVisibleTo`** in any production or test assembly.
  If a test needs to reach non-public code, refactor the production type to
  expose what's needed through a proper surface (interface, public helper,
  or a dedicated testable seam) rather than piercing encapsulation.

## Coding standards (repo-specific bits)

### C#

- Type aliases (`int`, `string`) — not `Int32` / `String`.
- Private fields `_camelCase`; public fields / properties / methods
  `PascalCase`; interfaces `I`-prefixed; locals `camelCase` with `var`.
- Class member order: fields → properties → ctors → methods → events.
  Fields grouped: const, static readonly, readonly, static, private, public.
- Explicit access modifiers on every declaration.
- Avoid multiple optional `bool` parameters — use overloads or named args.
- **File-scoped namespaces are required.** Block-scoped namespaces are
  configured as a **build error** — new and modified files must use
  `namespace Foo.Bar;` form.
- **Unused `using` directives fail the build.** Remove them before
  compiling; don't leave speculative imports in place.

### Wrapper projects: prefer `VisibilityBypasser`

In `src/Agent/NewRelic/Agent/Extensions/Providers/Wrapper/*`, reach for
`VisibilityBypasser` before `MethodInfo.Invoke`, `GetMethods`, or the
`dynamic` keyword. It generates IL-compiled delegates via `DynamicMethod`,
avoiding boxing / `object[]` allocation / DLR overhead on hot paths.

Cache generated delegates per instrumented type (e.g., a
`ConcurrentDictionary<Type, Func<...>>`) so IL generation happens once per
type. Useful overloads:

- `GeneratePropertyAccessor<TResult>(ownerType, propertyName)`
- `GenerateParameterlessMethodCaller<TResult>(...)`
- `GenerateOneParameterMethodCaller<TParam, TResult>(assemblyName, typeName, methodName)`

When the owner type is only known at runtime, use
`t.Assembly.GetName().Name` and `t.FullName`; wrap the cache factory in
`try/catch` for missing members. `dynamic` / `MethodInfo.Invoke` are fine
only when no suitable overload exists *and* the call is not on a hot path.

### C++ (profiler)

- WebKit C++ style guide.
- Compact namespaces: `namespace NewRelic::Profiler::Logger { ... }`.
- `.clang-format` at the profiler solution root is authoritative.

## Agent skills

### Issue tracker

Issues are tracked in Jira (New Relic, project `NR`) via the Atlassian MCP;
the GitHub repo is a public code/PR mirror. External PRs are not a triage
surface. See `docs/agents/issue-tracker.md`.

### Triage labels

Canonical role strings used as-is as Jira labels. See
`docs/agents/triage-labels.md`.

### Domain docs

Single-context: one `CONTEXT.md` + `docs/adr/` at the repo root. See
`docs/agents/domain.md`.

