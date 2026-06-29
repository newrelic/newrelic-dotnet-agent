---
name: build-dotnet-agent
description: Build the New Relic .NET agent locally and validate that code changes compile. Use this whenever you are about to build the agent, need to confirm your edits compile, need to refresh the agent home directories (newrelichome_*) before running tests, or find yourself reaching for `dotnet build` / `msbuild` on any project in this repo. Always build the full solution from the CLI in Debug -- never a single project. Covers that command and the traps that waste time (building individual projects, Release config, building the profiler by hand, dragging preview SDKs into build tools).
---

# Building the .NET agent

To validate edits or refresh agent home dirs, build the full solution yourself from the repo root:

```bash
dotnet build FullAgent.sln
```

Debug is default -- never pass `-c Release`. It's slow; run in background or tee to a file and read the tail. The user builds in VS; that's their path, not yours -- you use the CLI.

## Rules

- **Never build a single `.csproj`.** `Core.csproj` and its dependents have a post-build step (ILRepack + `AssemblyModifier.exe`) whose ordering only the solution provides. Building one project alone yields `AssemblyModifier.exe not found`, exit code 3, or `$(SolutionDir)`/`*Undefined*` errors.
- **Debug, never Release** (Release is for CI/releases).
- **`Core.UnitTest` from the CLI needs `SolutionDir`** (VS sets it, `dotnet` doesn't), trailing backslash required. Both forms below pass the same value (`<repo-root>\`):
  ```bash
  # bash (Git Bash)
  SLN="$(cygpath -w "$PWD")\\"
  dotnet build tests/Agent/UnitTests/Core.UnitTest/Core.UnitTest.csproj -f net10.0 -p:SolutionDir="$SLN"
  ```
  ```powershell
  # PowerShell
  $sln = "$((Resolve-Path .).Path)\"
  dotnet build tests/Agent/UnitTests/Core.UnitTest/Core.UnitTest.csproj -f net10.0 -p:SolutionDir="$sln"
  ```
- **`NewRelic.Agent.Extensions.Tests`:** `dotnet test` on the csproj silently no-ops via VSTestTask -- test the built DLL instead (path in root CLAUDE.md).
- **Profiler** (`Profiler.sln`, `src/Agent/NewRelic/Profiler/`, only when changing the profiler): build with `build.ps1`, never raw MSBuild; Debug; Linux build needs Docker Desktop.
- **Build tools stay on latest LTS** (`net10.0`): `build/*` tools (ArtifactBuilder, Dotty, etc.) and `AssemblyModifier`. When evaluating a preview .NET, retarget agent core + tests, not the build tools.

## Agent home directories

`FullAgent.sln` writes these under `src/Agent/`; all integration/unbounded/container tests read from them, so a stale home dir = "my change isn't taking effect." Rebuild after changing agent/wrapper code. If new instrumentation isn't applied, confirm files exist under `<agent-home>/extensions/` (and `extensions/netcore/` for .NET Core wrappers).

| Framework | OS  | Arch  | Directory |
|-----------|-----|-------|-----------|
| .NET FW   | Win | x64   | `newrelichome_x64` |
| .NET FW   | Win | x86   | `newrelichome_x86` |
| .NET Core | Win | x64   | `newrelichome_x64_coreclr` |
| .NET Core | Win | x86   | `newrelichome_x86_coreclr` |
| .NET Core | Linux | x64 | `newrelichome_x64_coreclr_linux` |
| .NET Core | Linux | arm64 | `newrelichome_arm64_coreclr_linux` |
