---
name: run-integration-tests
description: Run the New Relic .NET agent integration tests locally. Use whenever you need to run, debug, or reproduce an integration test (host-run IntegrationTests, UnboundedIntegrationTests, or ContainerIntegrationTests), are verifying an agent/wrapper change end to end, or find yourself reaching for `dotnet test` against a test project in this repo. You run these from the CLI yourself -- secrets and infra are already in place. Covers the build-first prerequisite, picking the layer, the run command per layer, and the env gotchas behind confusing failures.
---

# Running integration tests locally

Start a real app with the agent attached, exercise it, wait for a harvest, assert on the parsed agent log. Flow: build agent (if changed) -> build test solution (if changed) -> run via the layer's runner -> read results.

## 1. Build (solutions only, never individual projects)

**a. Agent -- only if agent/wrapper code changed.** Tests read built home dirs `src/Agent/newrelichome_*`, not source; stale home = #1 false failure. Build `FullAgent.sln` (see **build-dotnet-agent**). Already current for this branch? Skip.

**b. Test solution -- once, only if test/app/agent code changed since the last test-solution build.** One build serves all later runs; never rebuild between tests. Building the `.sln` builds the test project, shared `MultiFunctionApplicationHelpers`, and every launched app (the test `.csproj` doesn't reference the apps, but the solution includes all `Applications/*` + `SharedApplications/ConsoleMultiFunctionApplication{FW,Core}`) -- so never build an app by hand. App sourcing at test time: web apps copied from a `Deploy/` folder; FW console from `bin/<Config>/<tfm>`; Core console `dotnet publish`ed from source (always current).

Use full VS `MSBuild.exe` (not `dotnet build` -- legacy non-SDK web projects have `WebPublish` targets the SDK MSBuild can't build), Debug (fixture looks for apps under `Debug`). These flags mirror CI (`all_solutions.yml`) and are the only proven-good set: `-p:DeployOnBuild=true -p:PublishProfile=LocalDeploy` drives the web publish, and **no `-t:Build`** (the web csproj's `<Target Name="Deploy" AfterTargets="Build">` re-invokes `WebPublish`; `-t:Build` -> `error MSB4006: circular dependency ... "Deploy"`). Slow -- background/tee. Swap the `.sln` per layer.

```bash
MSBUILD=$("/c/Program Files (x86)/Microsoft Visual Studio/Installer/vswhere.exe" \
  -latest -prerelease -products '*' -requires Microsoft.Component.MSBuild \
  -find 'MSBuild/**/Bin/MSBuild.exe')
"$MSBUILD" tests/Agent/IntegrationTests/IntegrationTests.sln \
  -restore -m -p:Configuration=Debug -p:DeployOnBuild=true -p:PublishProfile=LocalDeploy
```

## 2. Pick the layer

| Layer | Solution / test project | Notes |
|-------|-------------------------|-------|
| **Host-run** (default) | `IntegrationTests.sln` / `IntegrationTests.csproj` | Windows + home dirs; real NR staging collectors. FW web-app tests (`RemoteWebApplication`/HostedWebCore) need an **elevated (Administrator)** terminal; console (`ConsoleDynamicMethodFixture`) tests don't. |
| **Unbounded** | `UnboundedIntegrationTests.sln` / `.csproj` | Real DBs/brokers (MySQL, Postgres, SQL Server, Mongo, Redis, RabbitMQ, Kafka, Elasticsearch, Couchbase...). |
| **Container** | `ContainerIntegrationTests.sln` / `.csproj` | Docker Desktop; Linux-agent coverage. Runner differs -- section 3. |
| Performance | `PerformanceTests.sln` | `run-perf-test.py`, not `dotnet test`. Out of scope. |

Host-run answers most things. Secrets auto (user secrets on `Shared.csproj`, already set). Unbounded infra is always up (`restart: unless_stopped`) -- no `docker compose up`; check `UnboundedServices` only on a connection failure.

## 3. Run -- runner depends on the layer

xUnit v3 projects (OutputType `Exe`, Microsoft.Testing.Platform).

**Host-run / Unbounded: run the built exe** `<testproj>/bin/Debug/net10.0/NewRelic.Agent.<layer>.exe` (as CI/VS do). **Not `dotnet test`** -- the VSTest bridge is ~3x slower (measured 7 ASB classes: ~5.7 min exe vs ~17 min `dotnet test`). Filters (native xUnit v3; cross-type AND; `*` at start/end): `-namespace "<FQN>"`, `-class "*Suffix"`/`"FQCN"`, `-method "FQN.Method"`. Dry-run `-list classes` (`/json` to parse) before a slow run. One invocation spans everything in one assembly. Default `-parallel collections`, but effective concurrency ~2 -- fixtures serialize per-app publish/tool-install behind a lock (`DotnetTool`/`RemoteApplication`); don't force wider.

Run in a sub-agent (**Sonnet**; **Haiku** for one test) returning only: `TEST EXECUTION SUMMARY` (Total/Failed/Skipped), failing names + asserts, `NR-ERROR`/`NR-FATAL`. Never raw read/tail/wide-grep the agent log -- counts, capped width, `/tmp` distill.

```bash
cd tests/Agent/IntegrationTests/UnboundedIntegrationTests/bin/Debug/net10.0
EXE=./NewRelic.Agent.UnboundedIntegrationTests.exe
"$EXE" -namespace "NewRelic.Agent.UnboundedIntegrationTests.AzureServiceBus" -class "*CoreLatest" \
       -trx "C:/IntegrationTestWorkingDirectory/TestResults/asb.trx" > /tmp/asb.log 2>&1
```

Exe prints `=== TEST EXECUTION SUMMARY ===` (Total/Errors/Failed/Skipped/Time); `-trx` writes a TRX; the agent-log temp path is printed in output.

**Container: use `dotnet test`** (runner is noise -- docker build + harvest waits dominate; ~2m16s `dotnet test` vs ~2m46s exe, same test). Multi-targets net10.0/net11.0 so pass `--framework`; filter by `[Trait]` like `linux_container_tests.yml`. Needs Docker Desktop + Linux home `src/Agent/newrelichome_x64_coreclr_linux` (`libNewRelicProfiler.so`); x64 needs no emulation, arm64 needs QEMU.

```bash
dotnet test tests/Agent/IntegrationTests/ContainerIntegrationTests/ContainerIntegrationTests.csproj \
  --framework net10.0 --no-build --filter "Architecture=amd64&Distro=Ubuntu"
#   ... --filter "FullyQualifiedName~UbuntuX64ContainerTest"   # one class
```

## 4. Logging env -- don't touch

`NEW_RELIC_LOG_DIRECTORY`/`NEW_RELIC_LOG_LEVEL` are inherited from the user's env -- don't set/override. Change agent behavior via `NewRelicConfigModifier`/`WebConfigModifier`/`fixture.SetEnvironmentVariable`, never ad-hoc XML.

## 5. On failure, check in order

1. **Elevated terminal?** FW web-app tests (RemoteWebApplication/HostedWebCore) need Administrator; else HostedWebCore exits code 2.
2. **Builds current?** `FullAgent.sln` fresh (if agent changed) + test solution built Debug with no app erroring out. (A stale `bin/Release/<tfm>/<rid>/publish` from `NR_DOTNET_TEST_PREBUILT_APPS=1` can mask source changes -- delete it.)
3. Fixture env vars set? 4. Ports free (localhost)? 5. Agent log: grep `NR-ERROR`/`NR-FATAL`, distilled. 6. App stdout/stderr.

Container: Docker up with resources, image built, container stayed up, host<->container reachable.
