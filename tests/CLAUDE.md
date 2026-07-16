# Tests

Layout, conventions, and the non-obvious facts for **writing** tests.
- **Running** integration tests -> `run-integration-tests` skill (build-first, layer pick, CLI, env gotchas, troubleshooting). Don't duplicate it here.
- **Building** first -> `build-dotnet-agent` skill. CLI build workarounds (`Core.UnitTest` `SolutionDir`, Extensions DLL-direct) and the no-unit-tests-for-wrappers rule -> [root claude.md](../CLAUDE.md).

Five layers, all integration layers read the built `src/Agent/newrelichome_*` dirs (build `FullAgent.sln` first):

| Layer | Solution | Needs |
|-------|----------|-------|
| Unit | (in `tests/Agent/UnitTests/`) | nothing |
| Integration (host-run) | `IntegrationTests.sln` | Windows + home dirs |
| Unbounded | `UnboundedIntegrationTests.sln` | real DB/broker infra |
| Container | `ContainerIntegrationTests.sln` | Docker Desktop (Linux-agent coverage) |
| Performance | `PerformanceTests.sln` | Python-driven, not `dotnet test` |

## Layout

```
tests/Agent/
├── UnitTests/
│   ├── Core.UnitTest/                     # agent core (needs SolutionDir from CLI)
│   ├── NewRelic.Agent.Extensions.Tests/   # shared Extensions helpers (NOT wrappers)
│   ├── CompositeTests/                    # cross-component flows
│   ├── NewRelic.Agent.TestUtilities/      # mock/config builders, data generators
│   ├── AsyncLocalTests/  ParsingTests/    # async-context; SQL/config/JSON parsers
│   └── PublicApiChangeTests/              # public-API stability gate
├── IntegrationTests/
│   ├── IntegrationTests/                  # host-run
│   ├── ContainerIntegrationTests/  UnboundedIntegrationTests/
│   ├── UnboundedServices/                 # docker-compose for unbounded infra
│   ├── Applications/                      # host-run FW/Core test apps
│   ├── ContainerApplications/             # Docker test apps
│   ├── UnboundedApplications/             # external-infra test apps
│   ├── SharedApplications/                # MFA hosts + exercisers (see below)
│   ├── IntegrationTestHelpers/            # fixtures, log parsers, wire models
│   └── Models/                            # telemetry wire-model types
└── NewRelic.Testing.Assertions/           # metric/trace/span/event asserts
```

## Unit tests

- NUnit primary; a few xUnit.
- **JustMock Lite** (free tier): interfaces + virtual members only -- no sealed/static/non-virtual mocking. Design new code with interfaces + virtual methods so it stays mockable.
- Helpers: `NewRelic.Agent.TestUtilities` (builders, generators), `NewRelic.Testing.Assertions` (telemetry asserts).
- **`PublicApiChangeTests`** gates the public API surface; an intentional break needs an explicit baseline update.
- **Adding a config property?** A member added to `IConfiguration` and surfaced in `ReportedConfiguration` (the serialized connect/settings payload) breaks two tests that assert against **hard-coded expected JSON**: `DataTransport/AgentSettingsTests.cs` and `DataTransport/ConnectModelTests.cs` (both `serializes_correctly`). Add the new `[JsonProperty(...)]` key to both, at the **same position** it sits in `ReportedConfiguration.cs` (ordered, not alphabetical), with the value `ExhaustiveTestConfiguration` returns. Failure reads as an expected-vs-actual string-length mismatch near the neighboring property -- easy to misread as unrelated.
- **Never `InternalsVisibleTo`** -- refactor the production type instead (root claude.md).

## Integration tests

Pattern: start a test app with the agent attached, exercise it, wait for a harvest, assert on the parsed agent log.

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

**Where new test apps go** (three parallel dirs under `IntegrationTests/`): `Applications/` (host-run FW/Core), `ContainerApplications/` (Docker), `UnboundedApplications/` (external infra, paired with `UnboundedServices/` compose). **Prefer the MFA pattern below over a new app.** Add a new `*Applications/` project only for a specific hosting model (IIS/OWIN, ASP.NET Core startup, WCF, Azure Functions, Lambda).

### MFA (Console MultiFunction App) pattern

Two shared console hosts dispatch string commands to **exerciser** classes; tests drive them via a `ConsoleDynamicMethodFixture*` fixture. Under `SharedApplications/`:
- `ConsoleMultiFunctionApplicationFW/` -- .NET Framework host.
- `ConsoleMultiFunctionApplicationCore/` -- .NET (Core) host.
- `Common/MultiFunctionApplicationHelpers/` -- all exercisers, grouped by library. Its csproj pins **oldest/minimum** supported package versions per TFM.
- `Common/MFALatestPackages/` -- parallel csproj pinning **latest** versions. **Bumping a library to test a newer version edits this file, not the helpers csproj.**

**Exerciser shape:** `[Library]` on the class, `[LibraryMethod]` on each command entry point, at least one `[Transaction]` method so the agent initializes (example: `NetStandardLibraries/StackExchangeRedisExerciser.cs`). Drive it with `_fixture.AddCommand("ExerciserClass Method arg1 arg2")` then `_fixture.Initialize()`.

**Gotchas:**
- Dispatcher matches command -> method by parameter **count, not type** -- do not overload `[LibraryMethod]` methods; they fail silently.
- Non-static exercisers need a parameterless ctor (instantiated by reflection).
- Exercisers must live in the helpers project; external assemblies aren't resolved by the reflection loader unless directly referenced.
- Use `Log.Info` / `Log.Error` inside exercisers -- output is timestamped and captured in test logs.

**Fixture variants:** treat `IntegrationTestHelpers/RemoteServiceFixtures/ConsoleDynamicMethodFixture.cs` as authoritative and grep it -- the set drifts as .NET versions roll. Currently FW (`FW462/471/48/481`, `FWLatest`, `FWSpecificVersion`) and Core (`Core80/100`, `CoreOldest/CoreLatest`, `CoreSpecificVersion`), with `AIM`/`HSM`/`CSP` security-mode suffixes on the `Latest` fixtures. To run one scenario across runtimes, make the test class generic on the fixture type and derive concrete classes bound to each variant.

### Key fixture types (`IntegrationTestHelpers/`)

- `RemoteApplication` -- base for app fixtures; owns lifecycle, env-var config, log collection.
- `AgentLogFile` -- parses the agent log produced by the run (the assertion source).
- Wire models (`MetricWireModel`, `TransactionTraceWireModel`, `SpanEventWireModel`, ...) for typed assertions.

### Configuring the agent in a test

**Always** route `newrelic.config` changes through `NewRelicConfigModifier` (and `WebConfigModifier` for ASP.NET FW `web.config`). **Never edit the XML ad hoc** from a test. If the setting you need has no method, **add one** -- that keeps the supported config surface visible in one place.

### Collectors and harvest cycles

Integration tests connect to **real New Relic staging collectors** with a shared test license key; the agent harvests for real and assertions read the harvested payloads back from the agent log. The few tests needing deterministic collector behavior (response-handling, connect-flow) use the `MockNewRelic` fixture (`Applications/MockNewRelic/`). Default new tests to staging unless you must simulate collector-side behavior.

Staging's connect response sets `event_harvest_config.report_period_ms=5000`, so **transaction/log/error/custom events already harvest every 5s** -- no override needed. **Metrics** use a separate 60s cycle -> `ConfigureFasterMetricsHarvestCycle` is still required for metric assertions. **Span events** use `span_event_harvest_config` at 60s -> `ConfigureFasterSpanEventsHarvestCycle` when asserting on spans.

### test.runsettings scope

The repo-root `test.runsettings` holds only NUnit naming settings and is auto-applied to **unit-test** projects via `RunSettingsFilePath` in their csproj (don't pass `--settings` by hand). It is **not** wired into integration projects, and CI runs those via the built exe (`NewRelic.Agent.IntegrationTests.exe -namespace ...`), so no run-settings file applies there.

### Container test mechanics (ContainerIntegrationTests)

How the Linux-agent container tests actually wire up (rediscovered often -- captured here):

- **Harness chain:** `ContainerTestFixtureBase : RemoteApplicationFixture` wraps
  `ContainerApplication : RemoteApplication`. `ContainerApplication` runs
  `docker compose -f <composeFile> -p <name> up --build --abort-on-container-exit ...`
  against service `LinuxSmokeTestApp`. Ctor:
  `ContainerApplication(distroTag, arch, dotnetVersion, dockerfile, dockerComposeFile = "docker-compose.yml", serviceName = "LinuxSmokeTestApp")`.
- **Compose files** live in `tests/Agent/IntegrationTests/ContainerApplications/`. `docker-compose.yml`
  extends `docker-compose-base.yml`'s `base-app`, which declares build `args:` (DISTRO_TAG, TARGET_ARCH,
  BUILD_ARCH, NEW_RELIC_LICENSE_KEY/APP_NAME/HOST, DOTNET_VERSION, ...), `ports: ${PORT}:80`, and two bind
  mounts: `${AGENT_PATH}:/usr/local/newrelic-dotnet-agent` (the agent home) and `${LOG_PATH}:/app/logs` (logs).
  The base has **no `environment:` block**.
- **Which Dockerfile:** the fixture picks it via the `dockerfile` field (e.g. `SmokeTestApp/Dockerfile`,
  `.centos`, `.amazon`, `.fedora`), passed to compose as `TEST_DOCKERFILE`. `SmokeTestApp` is a plain ASP.NET
  Core app exposing `GET /weatherforecast`; its Dockerfile bakes in `CORECLR_ENABLE_PROFILING`,
  `CORECLR_PROFILER`, `CORECLR_NEW_RELIC_HOME=/usr/local/newrelic-dotnet-agent`, `CORECLR_PROFILER_PATH`, and
  `NEW_RELIC_LOG_DIRECTORY=/app/logs`. The agent attaches on boot and logs its startup lines with no app code.
- **Passing a per-test container env var** (e.g. `NEW_RELIC_DISABLE_APPDOMAIN_CACHING`): `RemoteApplicationFixture
  .SetAdditionalEnvironmentVariable(k,v)` lands the var in the `docker compose up` **host process** env only --
  compose forwards it into the container **only if** a compose file references it via `${VAR}` in an
  `environment:` entry. The base compose has none, so add a small compose override file that `extends` `base-app`
  and adds `environment: - VAR=${VAR:-}` (pattern: `docker-compose-awssdk.yml` uses `- DEBUG=${DEBUG:-0}`), then
  point the fixture's `dockerComposeFile` at it. Fixtures that need a custom compose file extend
  `RemoteApplicationFixture` directly and thread `dockerComposeFile` through the `ContainerApplication` ctor
  (pattern: `KafkaTestFixtureBase`) -- `ContainerTestFixtureBase` hardcodes the default `docker-compose.yml`.
- **Reading logs / assertions:** `LOG_PATH` = `RemoteApplication.DefaultLogFileDirectoryPath` (host), bind-mounted
  to `/app/logs`, so `_fixture.AgentLog` (`AgentLogFile`) and `_fixture.ProfilerLog` (`ProfilerLogFile`,
  `GetFullLogAsString()`) read the container's logs on the host with no container/host distinction. Set
  `ProfilerLogExpected = true` in the fixture ctor when asserting on the profiler log. The profiler startup line
  `Calls to the managed agent will use the calling strategy - <X>` is visible here, same as the host tests.
- **Templates:** simplest fixture+test pair to copy = `Fixtures/LinuxUnicodeLogFileTestFixture.cs` +
  `Tests/LinuxUnicodeLogFileTest.cs` (asserts on `ProfilerLog`). `Tests/LinuxContainerTests.cs` shows the
  per-distro `[Trait("Architecture",...)] [Trait("Distro",...)]` class fan-out and the
  `Actions(setupConfiguration, exerciseApplication)` -> `Initialize()` flow.
- **glibc vs musl:** `build.ps1 -Platform linux` and the `homefolders` CI artifact only carry the **glibc**
  `.so` (`newrelichome_{x64,arm64}_coreclr_linux`); `CopyNewRelicHomeCoreClrLinuxDirectoryToRemote` copies that
  same glibc home for **every** distro fixture including Alpine (the separate musl profiler artifacts from
  `build_profiler.yml` are not wired into `homefolders`). Target **glibc distros (Ubuntu "noble")** for new
  functional tests; avoid Alpine unless specifically validating musl.

## Performance tests

Agent-overhead harness under `tests/Agent/PerformanceTests/` -- Python-orchestrated, not `dotnet test`. Components: `PerformanceTestApp/` (ASP.NET Core workload), `TrafficDriver/` (Locust, enforces <1% error rate), `ReportGenerator/` (ScottPlot charts + `summary.md`), `run-perf-test.py` (single run), `run-perf-comparison.py` (multiple configs from `compare.yml`). The runner bind-mounts an agent-home dir into the container at `/usr/local/newrelic-dotnet-agent` and sets `CORECLR_ENABLE_PROFILING` (0 for the no-agent baseline); `agent-home/` is repopulated and cleared between runs. Needs Docker Desktop (Linux containers), Python 3, `pip install pyyaml`. Full reference: [PerformanceTests/README.md](Agent/PerformanceTests/README.md).

## CI

GitHub Actions runs unit + integration tests on every PR via [`all_solutions.yml`](../.github/workflows/all_solutions.yml); coverage to Codecov.

## Related

- Skills: `run-integration-tests` (run them), `build-dotnet-agent` (build first)
- [root claude.md](../CLAUDE.md), [src/CLAUDE.md](../src/CLAUDE.md), [build/CLAUDE.md](../build/CLAUDE.md)
- [docs/integration-tests.md](../docs/integration-tests.md), [docs/development.md](../docs/development.md)
