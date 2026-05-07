# Testing Guide

Four test layers. See the [root claude.md](../claude.md) for CLI build /
test commands and for the no-unit-tests-for-wrappers rule.

- **Unit tests** — `tests/Agent/UnitTests/` (NUnit-primary, some xUnit)
- **Integration tests** — `IntegrationTests.sln`, host-run
- **Unbounded integration tests** — `UnboundedIntegrationTests.sln`, need
  external infra (databases, brokers)
- **Container integration tests** — `ContainerIntegrationTests.sln`, run
  instrumented apps inside Docker

All three integration solutions read from the built agent home
directories, so **build `FullAgent.sln` first** before running them.

## Layout

```
tests/
├── Agent/
│   ├── UnitTests/
│   │   ├── Core.UnitTest/                       # Agent core
│   │   ├── NewRelic.Agent.Extensions.Tests/     # Extensions shared code (not wrappers)
│   │   ├── CompositeTests/                      # Cross-component flows
│   │   ├── NewRelic.Agent.TestUtilities/        # Shared test helpers
│   │   ├── AsyncLocalTests/                     # Async-context behavior
│   │   ├── ParsingTests/                        # SQL / config / JSON parsers
│   │   └── PublicApiChangeTests/                # Public-API stability gate
│   ├── IntegrationTests/
│   │   ├── IntegrationTests/                    # Host-run tests
│   │   ├── ContainerIntegrationTests/           # Docker-based tests
│   │   ├── UnboundedIntegrationTests/           # Infra-backed tests
│   │   ├── UnboundedServices/                   # docker-compose for unbounded infra
│   │   ├── Applications/                        # Windows / .NET FW + .NET Core test apps
│   │   ├── ContainerApplications/               # Linux / container test apps
│   │   ├── UnboundedApplications/               # Apps exercising external infra
│   │   ├── SharedApplications/                  # Shared app code
│   │   ├── ApplicationHelperLibraries/          # Helper libs consumed by test apps
│   │   ├── IntegrationTestHelpers/              # Fixtures, log parsers, wire models
│   │   └── Models/                              # Telemetry wire-model types
│   ├── Shared/
│   │   ├── TestSerializationHelpers/
│   │   └── TestSerializationHelpers.Test/
│   └── NewRelic.Testing.Assertions/             # Custom asserts (metrics, traces, spans, events)
└── NewRelic.Core.Tests/
```

## Unit tests

- Primary framework: **NUnit**. A few projects use xUnit.
- Mocking: **Telerik JustMock Lite** (free tier) — interfaces and virtual
  members only. No sealed / static / non-virtual mocking. Design new code
  with interfaces + virtual methods so it stays mockable.
- Helpers: `NewRelic.Agent.TestUtilities` (mock builders, config builders,
  data generators) and `NewRelic.Testing.Assertions` (metric / trace /
  span / event asserts).
- **`PublicApiChangeTests`** compares the current public API surface to a
  baseline; breaking changes intentional? Update the baseline explicitly.
- **Never use `InternalsVisibleTo`** to reach non-public code — refactor
  the production type instead (see root claude.md).

CLI build/test commands — including the `-p:SolutionDir=…` workaround for
`Core.UnitTest` and the DLL-direct workaround for
`NewRelic.Agent.Extensions.Tests` — are in the [root claude.md](../claude.md).

## Integration tests

**Pattern:** start a test application with the agent attached, exercise
its endpoints, wait for a harvest, then assert on the parsed agent log.

```csharp
public class BasicMvcTests : NewRelicIntegrationTest<AspNetFrameworkBasicMvcApplication>
{
    private readonly AspNetFrameworkBasicMvcApplication _fixture;

    public BasicMvcTests() => _fixture = new AspNetFrameworkBasicMvcApplication();

    [Test]
    public void HomeIndexCreatesWebTransaction()
    {
        var result = _fixture.Get("Home/Index");
        var metrics = _fixture.AgentLog.GetMetrics();

        Assert.Multiple(() =>
        {
            Assert.That(result.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(metrics, Does.Contain("WebTransaction/MVC/Home/Index"));
        });
    }
}
```

**Where new test apps go** — three parallel directories under
`tests/Agent/IntegrationTests/`:
- `Applications/` — host-run .NET Framework / .NET apps for standard
  integration tests.
- `ContainerApplications/` — apps that run inside Docker for container
  integration tests (Linux agent coverage, container-specific scenarios).
- `UnboundedApplications/` — apps that exercise external infrastructure
  (DBs, brokers) and are paired with `UnboundedServices/` docker compose.

**Prefer the MFA (Console MultiFunction App) pattern over writing a new
test app.** Only create a new `*Applications/` project when the scenario
requires a specific hosting model (IIS/OWIN, ASP.NET Core startup, WCF,
Azure Functions, Lambda, etc.).

### Console MultiFunction App (MFA) pattern

Two shared console hosts dispatch string commands to **exerciser**
classes; tests drive them via a `ConsoleDynamicMethodFixture*` fixture.

**Projects** under `tests/Agent/IntegrationTests/SharedApplications/`:
- `ConsoleMultiFunctionApplicationFW/` — .NET Framework host.
- `ConsoleMultiFunctionApplicationCore/` — .NET (Core) host.
- `Common/MultiFunctionApplicationHelpers/` — all exercisers live here,
  grouped by instrumented library. Its csproj pins **oldest / minimum
  supported** package versions per TFM (net462/471/48/481/8.0/10.0).
- `Common/MFALatestPackages/` — parallel csproj pinning **latest**
  versions (net481, net10.0). **When bumping a library to test a newer
  version, edit this file, not the helpers csproj.**

**Exerciser shape:** `[Library]` on the class, `[LibraryMethod]` on each
command entry point. Include at least one `[Transaction]`-marked method
so the agent initializes. Example: `NetStandardLibraries/StackExchangeRedisExerciser.cs`.

Drive from a test: `_fixture.AddCommand("ExerciserClassName MethodName arg1 arg2")`
then `_fixture.Initialize()`.

**Fixture variants** (in `IntegrationTestHelpers/RemoteServiceFixtures/ConsoleDynamicMethodFixture.cs`):
- FW: `FW462`, `FW471`, `FW48`, `FW481`, `FWLatest`
- Core: `Core80`, `Core100`, `CoreOldest`, `CoreLatest`
- Security-mode suffixes on `Latest` fixtures: `AIM` / `HSM` / `CSP`.

To exercise the same scenario across runtimes, make the test class
generic on the fixture type and derive concrete classes bound to
`ConsoleDynamicMethodFixtureCoreLatest`, `…CoreOldest`, `…FWLatest`, etc.

**Gotchas:**
- Dispatcher matches commands to methods by parameter **count**, not
  type — do not overload `[LibraryMethod]` methods, they fail silently.
- Non-static exerciser classes need a parameterless constructor
  (instantiated by reflection).
- Exercisers must be in the helpers project; external assemblies aren't
  resolved by the reflection-based loader unless directly referenced.
- Use `Log.Info` / `Log.Error` inside exercisers — output is timestamped
  and captured in test logs.

**Key fixture types** (in `IntegrationTestHelpers/`):
- `RemoteApplication` — base for test-app fixtures; owns app lifecycle,
  env-var config, and log collection.
- `AgentLogFile` — parses the agent log produced by the test run.
- Wire models (`MetricWireModel`, `TransactionTraceWireModel`,
  `SpanEventWireModel`, etc.) for strongly-typed assertions.

**Configuring the agent in a test:** per-app `newrelic.config`, plus env
vars via `fixture.SetEnvironmentVariable(...)`.

**Collectors:** integration tests connect to **real New Relic staging
collectors** using a shared test license key. The agent actually harvests
and emits data; assertions then read telemetry back from the agent log
(harvested payloads are captured there as well as sent). A small number
of tests that need deterministic collector behavior — response-handling
and connect-flow tests — use the `MockNewRelic` fixture
(`tests/Agent/IntegrationTests/Applications/MockNewRelic/`) which stands
in for the collector locally. If you're writing a new test, default to
the staging collector unless you specifically need to simulate
collector-side behavior.

**Always use `NewRelicConfigModifier`** (in `IntegrationTestHelpers/`) to
tweak `newrelic.config` for a test. **Never manipulate the XML directly**
from a test class. If the setting you need doesn't already have a method
on `NewRelicConfigModifier`, **add one** — don't work around it with
ad-hoc XML edits. There is a parallel `WebConfigModifier` for
`web.config` changes in ASP.NET Framework test apps; same rule applies.
Keeping all config mutation routed through these helpers makes test
setup readable and the supported config surface visible in one place.

**Running from the CLI:**
```powershell
dotnet test tests/Agent/IntegrationTests/IntegrationTests/IntegrationTests.csproj
dotnet test --filter "FullyQualifiedName~BasicMvcTests"
dotnet test --filter "Category=AspNetCore"

# or via the bundled PowerShell helper
.\build\Scripts\run-integration-tests.ps1
```

**`test.runsettings`** at the repo root sets timeout, parallelization, and
data collectors:
```powershell
dotnet test --settings test.runsettings
```

## Container integration tests

Under `tests/Agent/IntegrationTests/ContainerIntegrationTests/` with apps
in `ContainerApplications/`. This is the primary coverage for the Linux
agent and container-specific scenarios. Requires Docker Desktop.

```powershell
dotnet test tests/Agent/IntegrationTests/ContainerIntegrationTests/ContainerIntegrationTests.csproj
```

## Unbounded integration tests

Tests that exercise real infrastructure — MySQL, PostgreSQL, SQL Server,
MongoDB, Redis, RabbitMQ, Kafka, Elasticsearch, Couchbase, and more.

Start the services first:
```powershell
cd tests/Agent/IntegrationTests/UnboundedServices
docker compose up -d
```

Then:
```powershell
dotnet test tests/Agent/IntegrationTests/UnboundedIntegrationTests/UnboundedIntegrationTests.csproj
```

## Troubleshooting

**Integration test failures — check in this order:**
1. Did `FullAgent.sln` build successfully?
2. Are the `src/Agent/newrelichome_*` directories present and up to date?
3. Are the expected env vars set on the fixture?
4. Ports free (test apps bind to localhost)?
5. Agent log — each test writes one under a temp path that appears in the
   test output; grep for `NR-ERROR` / `NR-FATAL` first.
6. Test-application stdout/stderr captured by the fixture.

**Container test failures — check:**
1. Docker Desktop running with enough CPU / memory / disk?
2. Did the container image build?
3. Did the container actually start, and is it still running?
4. Network reachable between the test host and the container?

## CI

GitHub Actions runs unit + integration tests on every PR via
[`.github/workflows/all_solutions.yml`](../.github/workflows/all_solutions.yml).
Code coverage goes to Codecov (badge in the repo README).

## Related docs

- [Main repository guide](../claude.md)
- [Source architecture](../src/claude-source.md)
- [Build system](../build/claude-build.md)
- [Integration test details](../docs/integration-tests.md)
- [Development guide](../docs/development.md)
