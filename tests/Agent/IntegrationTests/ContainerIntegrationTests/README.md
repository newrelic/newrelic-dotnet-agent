# Adding a new container integration test

This solution runs instrumented Linux test applications inside Docker containers to validate the New Relic .NET agent's Linux support (see [Container test mechanics](../../../CLAUDE.md#container-test-mechanics-containerintegrationtests) in `tests/CLAUDE.md` for the underlying harness, and [docs/integration-tests.md](../../../../docs/integration-tests.md) for how this solution fits among the other test layers). Running the tests locally requires Docker Desktop.

There are two ways to add a new test, depending on what you need to exercise.

## (A) Reuse the existing SmokeTestApp

If your test just needs *any* ASP.NET Core app with the agent attached — e.g. a profiler/log-format or app-domain-caching behavior check — you don't need a new test application, Dockerfile, or compose file. Copy the pattern from `Fixtures/LinuxUnicodeLogFileTestFixture.cs` + `Tests/LinuxUnicodeLogFileTest.cs`:

1. **Fixture** — derive from `ContainerTestFixtureBase`:

   ```csharp
   public class LinuxUnicodeLogFileTestFixture : ContainerTestFixtureBase
   {
       private const string Dockerfile = "SmokeTestApp/Dockerfile";
       private const string DockerComposeServiceName = "LinuxUnicodeLogfileTestApp";
       private const ContainerApplication.Architecture Architecture = ContainerApplication.Architecture.X64;
       private const string DistroTag = "noble";

       public LinuxUnicodeLogFileTestFixture() : base(DistroTag, Architecture, Dockerfile)
       {
           ProfilerLogExpected = true; // only needed if you assert on the profiler log
       }
   }
   ```

2. **Test** — derive from `NewRelicIntegrationTest<TFixture>`, wire up `Actions(setupConfiguration, exerciseApplication)`, then `Initialize()`:

   ```csharp
   [Trait("Architecture", "amd64")]
   [Trait("TestArea", "Core")]
   public class LinuxUnicodeLogFileTest : NewRelicIntegrationTest<LinuxUnicodeLogFileTestFixture>
   {
       private readonly LinuxUnicodeLogFileTestFixture _fixture;

       public LinuxUnicodeLogFileTest(LinuxUnicodeLogFileTestFixture fixture, ITestOutputHelper output) : base(fixture)
       {
           _fixture = fixture;
           _fixture.TestLogger = output;

           _fixture.Actions(setupConfiguration: () =>
               {
                   var configModifier = new NewRelicConfigModifier(_fixture.DestinationNewRelicConfigFilePath);
                   configModifier.ConfigureFasterMetricsHarvestCycle(10);
               },
               exerciseApplication: () =>
               {
                   _fixture.ExerciseApplication();
                   _fixture.Delay(11);
                   _fixture.AgentLog.WaitForLogLine(AgentLogBase.MetricDataLogLineRegex, TimeSpan.FromSeconds(11));
                   _fixture.ShutdownRemoteApplication();
                   _fixture.AgentLog.WaitForLogLine(AgentLogBase.ShutdownLogLineRegex, TimeSpan.FromSeconds(10));
               });

           _fixture.Initialize();
       }

       [Fact]
       public void Test()
       {
           // assert against _fixture.AgentLog / _fixture.ProfilerLog
       }
   }
   ```

3. Pick a `[Trait("TestArea", ...)]` — see [Choosing a TestArea](#choosing-a-testarea-or-adding-a-new-one) below. **Never** add `[Trait("Distro", ...)]` for new functional coverage.

That's it — no solution, Docker, or CI changes are needed for this path (assuming you reuse an existing `TestArea`).

## (B) Add a new test application

Use this path when you're testing a specific library/integration (a new datastore client, message broker, AWS service, etc.) that needs its own application code and possibly its own dependency container(s) (a broker, a cache server, ...).

### 1. Create the test application project

Add a new folder under `tests/Agent/IntegrationTests/ContainerApplications/<NewApp>/` as an ASP.NET Core (`Microsoft.NET.Sdk.Web`) project. `MemcachedTestApp/MemcachedTestApp.csproj` is a good minimal template:

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFrameworks>net8.0;net10.0</TargetFrameworks>
    <DockerDefaultTargetOS>Linux</DockerDefaultTargetOS>
    <DockerfileContext>.</DockerfileContext>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.VisualStudio.Azure.Containers.Tools.Targets" Version="1.23.0" />
    <!-- add the package(s) under test here -->
  </ItemGroup>
</Project>
```

`ContainerApplications/Directory.Build.props` automatically chains to the parent `Directory.Build.props` for shared warnings/`NoWarn` settings, so you don't need to repeat that plumbing.

### 2. Add a Dockerfile

Add a `Dockerfile` in the new app folder. Copy an existing one (e.g. `MemcachedTestApp/Dockerfile`) as a starting point — it needs to build the app, install/copy the New Relic agent home directory mount point, and set `CORECLR_ENABLE_PROFILING` / `CORECLR_PROFILER` / `CORECLR_NEWRELIC_HOME` / `CORECLR_PROFILER_PATH` / `NEW_RELIC_LOG_DIRECTORY` as `SmokeTestApp/Dockerfile` does.

Only glibc-based distro images are exercised for functional tests by default — the CI build only produces glibc agent-home artifacts (`newrelichome_{x64,arm64}_coreclr_linux`), which get bind-mounted into every distro's container regardless of libc, so an Alpine (musl) container silently runs a mismatched agent binary. **Target Ubuntu ("noble")** for new functional coverage; only use Alpine if you are specifically validating musl support.

### 3. Add a docker-compose file

Add `ContainerApplications/docker-compose-<name>.yml`. It must `extends` the shared `base-app` service from `docker-compose-base.yml`. The minimal shape (from `docker-compose.yml`):

```yaml
services:
    LinuxSmokeTestApp:
        extends:
            file: docker-compose-base.yml
            service: base-app
        container_name: ${CONTAINER_NAME}
        image: ${CONTAINER_NAME}
        platform: ${PLATFORM}

networks:
    default:
        driver: bridge
        driver_opts:
          com.docker.network.bridge.enable_icc: "true"
```

**If your test needs a sidecar service** (a broker, cache server, etc.), add it as another service and make your app's service `depends_on` it, following `docker-compose-kafka.yml`:

```yaml
services:
    LinuxSmokeTestApp:
        extends:
            file: docker-compose-base.yml
            service: base-app
        container_name: ${CONTAINER_NAME}
        image: ${CONTAINER_NAME}
        platform: ${PLATFORM}
        depends_on:
            kafka-broker:
                condition: service_healthy
        environment:
            - NEW_RELIC_KAFKA_TOPIC=${NEW_RELIC_KAFKA_TOPIC}
            - NEW_RELIC_KAFKA_BROKER_NAME=kafka-broker

    kafka-broker:
        image: confluentinc/cp-kafka:7.5.0
        healthcheck:
            test: ["CMD", "kafka-broker-api-versions", "--bootstrap-server", "localhost:9092"]
            interval: 5s
            timeout: 5s
            retries: 10
            start_period: 15s
        environment:
          # ... broker config
```

**If your test needs a per-test environment variable passed into the container** (e.g. to toggle a feature flag), call `RemoteApplicationFixture.SetAdditionalEnvironmentVariable(key, value)` from your fixture. That lands the variable in the `docker compose up` **host process** environment only — compose forwards it into the container **only if** your compose file's `environment:` block references it via `${VAR}`. `docker-compose-base.yml` declares no `environment:` block, so add a small override compose file that `extends: base-app` and adds the variable, e.g.:

```yaml
services:
    LinuxSmokeTestApp:
        extends:
            file: docker-compose-base.yml
            service: base-app
        environment:
            - NEW_RELIC_DISABLE_APPDOMAIN_CACHING=${NEW_RELIC_DISABLE_APPDOMAIN_CACHING:-}
```

(pattern used by `docker-compose-appdomaincaching.yml` / `AppDomainCachingContainerTestFixtures.cs`), then point your fixture's `dockerComposeFile` constructor argument at the new file.

### 4. Add the new project and compose file to the solution

`ContainerIntegrationTests.csproj` does **not** reference the app projects — they're built and run by Docker, not by `dotnet test` — so this step is purely for Visual Studio convenience (VS won't show/build files that aren't in the `.sln`).

The easiest way is from Visual Studio: right-click the `ContainerApplications` solution folder → **Add > Existing Project...** for the new `.csproj`, and right-click the `_docker` solution folder → **Add > Existing Item...** for the new compose file. This automatically writes the correct GUIDs and `ProjectConfigurationPlatforms`/`NestedProjects` entries.

If you're editing `ContainerIntegrationTests.sln` by hand (no Visual Studio available), add:

- A new `Project(...)` entry (using the C# project type GUID `{9A19103F-16F7-4668-BE54-9A1E7A4F7556}`) with a freshly generated project GUID, plus matching `Debug|Any CPU` / `Debug|x64` / `Debug|x86` / `Release|*` lines under `ProjectConfigurationPlatforms`, plus a line under `NestedProjects` mapping the new project GUID to the `ContainerApplications` folder GUID (`{84D70574-4AC7-4EA7-AE52-832C3531E082}`) — copy the block for an existing app (e.g. `MemcachedTestApp`) and swap in new GUIDs.
- A new line inside the existing `_docker` folder's `ProjectSection(SolutionItems)` block for the new compose file, e.g. `ContainerApplications\docker-compose-<name>.yml = ContainerApplications\docker-compose-<name>.yml`.

### 5. Create the fixture

If your app fits the default shape used by `ContainerTestFixtureBase` (just needs a distro/architecture/Dockerfile choice against the default compose file), derive from it like `MemcachedTestFixtureBase`:

```csharp
public abstract class MemcachedTestFixtureBase : RemoteApplicationFixture
{
    protected override int MaxTries => 1;

    protected MemcachedTestFixtureBase(
        string distroTag,
        ContainerApplication.Architecture containerArchitecture,
        string dockerfile,
        string dotnetVersion,
        string dockerComposeFile = "docker-compose-memcached.yml") :
        base(new ContainerApplication(distroTag, containerArchitecture, dotnetVersion, dockerfile, dockerComposeFile, "MemcachedTestApp"))
    {
    }

    public virtual void ExerciseApplication()
    {
        var address = $"http://localhost:{Port}/memcached/";
        GetAndAssertStatusCode(address + "testallmethods", System.Net.HttpStatusCode.OK);
    }
}
```

If you need a custom compose file or extra constructor plumbing (as above), derive from `RemoteApplicationFixture` directly and thread `dockerComposeFile` through `ContainerApplication`'s constructor, following `KafkaTestFixtureBase` or `AwsSdkContainerTestFixtureBase`.

### 6. Create the test class(es)

One `[Fact]` per assertion set, tagged with `[Trait("Architecture", "amd64")]` and the appropriate `[Trait("TestArea", ...)]`. Assert against harvested metrics via `_fixture.AgentLog.GetMetrics()` and `NewRelic.Testing.Assertions.Assertions.MetricsExist(...)` — see `Tests/MemcachedTests.cs` for a complete example, including asserting on a metric whose full name isn't deterministic (`Datastore/instance/Memcached/<address>/11211`).

## Choosing a TestArea (or adding a new one)

Two trait axes select which tests a CI job runs, and they are **disjoint** — a class should carry exactly one combination that exactly one CI matrix entry asks for:

- **`[Trait("Distro", "...")]`** — OS-compatibility smoke tests **only** (current values: `Ubuntu`, `Alpine`, `Centos`, `Amazon`, `Fedora`). Do not add new functional coverage here — it doesn't scale.
- **`[Trait("TestArea", "...")]`** — functional test groupings (current values: `Core`, `Messaging`, `Aws`, `Datastore`). Reuse an existing value where your test fits.

Both traits may be declared on an abstract base class and inherited by concrete subclasses (e.g. `AwsSdkSQSTestBase` carries `TestArea=Aws`; its subclasses inherit it without redeclaring). Only concrete test classes need a selector — abstract bases never need one on their own, and a subclass's own trait wins over an inherited one.

## Wiring into CI

`dotnet test` discovers tests by trait at run time, so:

- **Reusing an existing `TestArea` (or `Distro`) value that already has a matrix entry in `.github/workflows/linux_container_tests.yml`** — no workflow change needed. Your new test class runs automatically in that job once merged.
- **Introducing a new `TestArea` value (or adding arm64 coverage for an area that's currently amd64-only)** — you must add a new `matrix.include` entry in the `linux-container-tests` job of `.github/workflows/linux_container_tests.yml`:

  ```yaml
          - arch: amd64
            name: MyNewArea
            runner: ubuntu-latest
            filter: "Architecture=amd64&TestArea=MyNewArea"
  ```

  Match `runner` to the architecture (`ubuntu-latest` for amd64, `ubuntu-24.04-arm` for arm64).

  **Nothing enforces this for you** — a test class whose trait combination no matrix entry asks for will silently never run in CI. Double-check your new class's traits against the matrix before opening a PR.

The workflow retries a failing job once (to absorb transient Docker/network blips) and has an `enhanced_logging` toggle for extra diagnostics — neither needs to change for a new test.

## Running locally

Build `FullAgent.sln` first (agent home directories must exist — see the `build-dotnet-agent` skill / [tests/CLAUDE.md](../../../CLAUDE.md)), make sure Docker Desktop is running, then run the same filtered command CI uses:

```
dotnet test ./tests/Agent/IntegrationTests/ContainerIntegrationTests/ContainerIntegrationTests.csproj \
  --framework net10.0 \
  --filter "Architecture=amd64&TestArea=Core"
```

See the `run-integration-tests` skill and [tests/CLAUDE.md](../../../CLAUDE.md) for the build-first prerequisite, test secrets setup, and general troubleshooting.

## Custom base images

Some distros (currently Amazon Linux and Fedora) use custom pre-built base images with the ASP.NET Core runtime already installed, to speed up test runs. If a new distro/.NET-version combination needs one, see [ContainerApplications/CustomBaseContainerBuild/README.md](../ContainerApplications/CustomBaseContainerBuild/README.md).

## See also

- [tests/CLAUDE.md](../../../CLAUDE.md) — container test mechanics, trait rules, and the other test layers
- [docs/integration-tests.md](../../../../docs/integration-tests.md) — how this solution fits among the other integration test layers
