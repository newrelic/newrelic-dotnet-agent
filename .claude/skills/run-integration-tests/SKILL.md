---
name: run-integration-tests
description: Run the New Relic .NET agent integration tests locally. Use this whenever you need to run, debug, or reproduce an integration test (host-run IntegrationTests, UnboundedIntegrationTests, or ContainerIntegrationTests), are verifying that an agent/wrapper change behaves correctly end to end, or find yourself reaching for `dotnet test` against a test project in this repo. You run these yourself from the CLI every time -- no human needed; secrets and infra are already in place. Covers the build-first prerequisite, picking the correct test layer (host-run, unbounded, container), the CLI command, and the environment gotchas that cause confusing failures.
---

# Running integration tests locally

Tests start a real app with the agent attached, exercise it, wait for a harvest, and assert on the parsed agent log. Run them yourself from the CLI every time.

## 1. Build first (two builds, in order)

**a. Build the agent.** Tests read the built home dirs (`src/Agent/newrelichome_*`), not your source. If agent/wrapper code changed, build `FullAgent.sln` first (see **build-dotnet-agent**) or the run uses stale agent code -- the #1 false failure. Don't debug a "failure" until you've confirmed a fresh build.

**b. Build only the app(s) your test launches.** The test launches its app as a separate process; the test `.csproj` does NOT reference the launched apps, so `dotnet test` never builds them (it only rebuilds the shared `MultiFunctionApplicationHelpers` library, a project reference). You must build the app yourself first. CI's **All Solutions Build** does a whole-solution `-p:DeployOnBuild=true` build because it runs the entire suite -- but locally you only need the one app the test(s) you're running use, so build that single project, not the solution.

**Find which app from the test's fixture** (the `NewRelicIntegrationTest<TFixture>` / `IClassFixture<TFixture>` generic arg, or the test's base class) -- deterministic, two cases:

- **Console fixture** (`ConsoleDynamicMethodFixture*` -- almost all unbounded + dynamic-method tests): the variant suffix gives the app + target framework. `FW*` -> `ConsoleMultiFunctionApplicationFW`, `Core*` -> `ConsoleMultiFunctionApplicationCore`. Current variant -> tfm (authoritative source is `ConsoleDynamicMethodFixture.cs`; it drifts as .NET versions roll, so grep it rather than trusting this snapshot): `FW462`->net462, `FW471`->net471, `FW48`->net48, `FW481`/`FWLatest`->net481, `Core80`/`CoreOldest`->net8.0, `Core100`/`CoreLatest`->net10.0.
- **Host-run web fixture** (lives in `IntegrationTests/RemoteServiceFixtures/`): the fixture's ctor names the app -- `grep -n "RemoteWebApplication(\|: base(\"" <fixture>.cs` yields e.g. `new RemoteWebApplication("BasicMvcApplication", ...)`. The project is `tests/Agent/IntegrationTests/Applications/<that name>/<that name>.csproj`.

Then build that single project. The command depends on the app type (this is the only reason full MSBuild ever matters):

- **Console MF app** (most unbounded / `ConsoleDynamicMethodFixture` tests) -- a plain SDK project; `dotnet build` is fine. Build it for the target framework your fixture variant uses:
  ```bash
  dotnet build tests/Agent/IntegrationTests/SharedApplications/ConsoleMultiFunctionApplicationFW/ConsoleMultiFunctionApplicationFW.csproj -c Debug -f net471
  # or ...ConsoleMultiFunctionApplicationCore/...Core.csproj -c Debug -f net10.0
  ```
- **Web app** (many host-run tests, e.g. `BasicMvcApplication`) -- a legacy web project that `dotnet build` cannot build/deploy; use full `MSBuild.exe` on just that project with its publish profile, which deploys it to its `Deploy` folder:
  ```bash
  "C:/Program Files/Microsoft Visual Studio/18/Enterprise/MSBuild/Current/Bin/MSBuild.exe" \
    -restore -p:Configuration=Debug -p:DeployOnBuild=true -p:PublishProfile=LocalDeploy \
    tests/Agent/IntegrationTests/Applications/BasicMvcApplication/BasicMvcApplication.csproj
  ```

How each launched app is then sourced at test time (`RemoteService.CopyToRemote`):

- **Web apps** -- copied from the `Deploy` folder produced by the publish profile.
- **.NET Framework console apps** -- plain file copy from `bin/<Config>/<tfm>` (NOT rebuilt at test time; your build above is what makes them current).
- **.NET (Core) console apps** -- `dotnet publish`ed from source at test time, so they pick up your latest source even without a prior build. (Exception: `NR_DOTNET_TEST_PREBUILT_APPS=1`, which CI sets, makes `TryCopyPrebuiltPublishOutput` copy a pre-published `bin/Release/<tfm>/<rid>/publish` output instead -- a stale one then masks changes; delete it if in doubt.)

**Config note:** the fixture reads `Utilities.Configuration` (`Debug` for a Debug-built test assembly, `Release` for Release). Match your app build config to how you run `dotnet test` -- Debug locally, hence `-p:Configuration=Debug` / `-c Debug` above.

## 2. Pick the layer

| Layer | Project | Notes |
|-------|---------|-------|
| **Host-run** (default) | `IntegrationTests/IntegrationTests.csproj` | Windows + built home dirs. Connects to real NR staging collectors with a shared test license key; asserts on the agent log. .NET Framework web app tests (those using `RemoteWebApplication` / HostedWebCore) **require an elevated (Administrator) terminal** -- HostedWebCore needs admin to bind ports via the IIS in-process API. Console app tests (`ConsoleDynamicMethodFixture`) do not. |
| **Unbounded** | `UnboundedIntegrationTests/UnboundedIntegrationTests.csproj` | Adds real DBs/brokers (MySQL, Postgres, SQL Server, Mongo, Redis, RabbitMQ, Kafka, Elasticsearch, Couchbase...). |
| **Container** | `ContainerIntegrationTests/ContainerIntegrationTests.csproj` | Docker Desktop. Linux-agent / container coverage. |
| Performance | `PerformanceTests.sln` | Not `dotnet test` (`run-perf-test.py`). Out of scope. |

Host-run answers almost everything; use unbounded/container only when the test needs that infra.

- **Secrets are automatic:** all secrets (incl. test license key) come from .NET user secrets on `tests/Agent/IntegrationTests/Shared/Shared.csproj`, already configured on this machine.
- **Unbounded infra is already up** (`restart: unless_stopped`) -- assume the DB/broker is present, no `docker compose up`. Only if a connection fails, check the `UnboundedServices` stack.
- **Container tests, two purposes, same execution:** (1) basic Linux agent functionality, (2) special cases (e.g. AWS SDK) that provision their own one-off infra. Both run identically with no extra setup.

## 3. Run it in a sub-agent

Don't run tests inline -- the runner output plus per-test agent log (hundreds of KB, long lines) would re-bill on every later turn. Dispatch a sub-agent (**Sonnet**; **Haiku** for a simple single-test pass/fail) and have it return only a short summary: pass/fail, failing assertion messages, any `NR-ERROR`/`NR-FATAL` lines.

Run `dotnet test` against the specific test `.csproj` (filtered). This rebuilds the test project and the shared library, then launches the app you built in section 1b (it does NOT rebuild that app). Run it in the same config you built the app (Debug):

```bash
dotnet test tests/Agent/IntegrationTests/IntegrationTests/IntegrationTests.csproj \
  --filter "FullyQualifiedName~BasicMvcTests"   # or --filter "Category=AspNetCore"
```

Secrets are present, so a failure is a real signal -- investigate (section 5), don't route around it.

**Large-log handling** (sub-agent, or you if reading directly): never raw `Read`/`tail`/wide-`grep` a big log -- it re-bills every turn. Counts before content (`grep -c`, `grep -o`, `sort|uniq -c`); cap width (`| cut -c1-200`); distill to a slim `/tmp` file and read that.

## 4. Don't touch the logging env

`NEW_RELIC_LOG_DIRECTORY` / `NEW_RELIC_LOG_LEVEL` are set in the user's environment and inherited -- do not set or override them. To change agent behavior in a test, use `NewRelicConfigModifier` / `WebConfigModifier` / `fixture.SetEnvironmentVariable(...)`, never ad-hoc XML edits.

## 5. On failure, check in order

1. **Elevated terminal?** If the test uses a .NET Framework web app (RemoteWebApplication/HostedWebCore), the terminal must be running as Administrator. HostedWebCore.exe exits with code 2 and the fixture fails with a process error if not elevated.
2. Did `FullAgent.sln` build and are `newrelichome_*` current? Did you build the app your test launches (section 1b) in the same config you're running `dotnet test`? (FW console + web apps are copied, not rebuilt by the test; a stale prebuilt Core publish output can also mask changes.) (most common)
2. Expected env vars set on the fixture?
3. Ports free? (apps bind localhost)
4. Agent log (temp path printed in test output): grep `NR-ERROR`/`NR-FATAL` first, distilled.
5. Test app stdout/stderr.

Container tests: Docker Desktop running with enough resources, image built, container started and stayed up, host<->container network reachable.
