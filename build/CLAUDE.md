# Build System

Tools that turn the solutions into shippable artifacts (NuGet, MSI, Linux
packages, Azure Site Extension, download-site bundle). See the
[root claude.md](../CLAUDE.md) for local-development build basics.

## Layout

```
build/
├── ArtifactBuilder/          # Orchestrates full-release builds
├── Packaging/                # Per-artifact packaging (NuGet, MSI, DownloadSite, AzureSiteExtension, ...)
├── Linux/                    # Linux .deb / .rpm build (Docker-driven)
├── Scripts/                  # PowerShell helpers (see below)
├── NewRelic.NuGetHelper/     # NuGet package utilities
├── NugetValidator/           # Validates built NuGet packages
├── NugetVersionDeprecator/   # Marks old NuGet versions deprecated
├── S3Validator/              # Validates uploaded artifacts on the download site
├── ReleaseNotesBuilder/      # Generates release notes from CHANGELOG + commits
├── Dotty/                    # .NET SDK version management helper
└── Tools/                    # Bundled toolchain (xsd2code, NuGet, NUnit-Console, XUnit-Console)
```

## ArtifactBuilder

The entry point for release builds. It drives `FullAgent.sln` + `Profiler.sln`,
copies profiler binaries into the agent home directories, assembles NuGet
packages, builds the MSI (when HeatWave is installed), and produces the
download-site bundle. Usually invoked by CI — rarely needed locally.

## Artifacts produced

**NuGet** (IDs verified against nuspecs under `build/Packaging/*`):
- `NewRelic.Agent` — full agent, all platforms
- `NewRelic.Agent.Api` — public API only (compile-time reference)
- `NewRelic.Agent.Internal.Profiler` — native profiler binaries
- `NewRelic.Azure.WebSites.x64` — Azure App Service, 64-bit
- `NewRelic.Azure.WebSites` — Azure App Service, 32-bit (no `.x86` suffix)
- `NewRelic.Azure.WebSites.Extension` — Azure Site Extension entry point
- `NewRelicWindowsAzure` — classic Azure Cloud Services

**MSI (`src/Agent/MsiInstaller/`):** `NewRelicAgent_x64.msi`,
`NewRelicAgent_x86.msi`. Build requires the HeatWave VS extension.

**Linux (`build/Linux/`):** `.deb` and `.rpm` for x64 and arm64. Installs
land at `/usr/local/newrelic-dotnet-agent/` with `newrelic.config` alongside.
Docker-driven via [Linux/docker-compose.yml](Linux/docker-compose.yml); see
[Linux/linux_packaging.md](Linux/linux_packaging.md) for the workflow.
Packaging sources live under `Linux/build/deb/`, `Linux/build/rpm/`, and
`Linux/build/common/`.

**Azure Site Extension (`Packaging/AzureSiteExtension/`):** installed via
the Azure Portal Extensions tab.

**Download site (`Packaging/DownloadSite/`):** structured directory of
zipped home directories, MSIs, Linux packages, and SHA-256 checksums,
ready to publish to `download.newrelic.com`.

## Scripts

PowerShell test-run helpers in [Scripts/](Scripts/):
- [Scripts/run-integration-tests.ps1](Scripts/run-integration-tests.ps1)
- [Scripts/run-platform-tests.ps1](Scripts/run-platform-tests.ps1)

Invoke from repo root, e.g.:
```powershell
.\build\Scripts\run-integration-tests.ps1
```

## Bundled tools (`Tools/`)

- `xsd2code/` — regenerates `Configuration.cs` from `Configuration.xsd`
  (command + license-header restore in
  [src/CLAUDE.md](../src/CLAUDE.md))
- `NUnit-Console/`, `XUnit-Console/` — CLI test runners
- `NuGet/`, `nuget.exe` — NuGet CLI
- `vswhere.exe` — Visual Studio discovery
- `sqlncli.msi` — SQL Server Native Client (MSI build dependency)

## MSBuild configuration

- **`Directory.Build.props`** at repo root and in subdirectories defines the
  agent version, `TreatWarningsAsErrors`, `LangVersion`, output paths, and
  analyzer rules. The agent version defined here propagates to NuGet
  package versions, the MSI product version, and Linux package versions.
- **`.editorconfig`** enforces indentation, line endings, naming
  conventions, and file-scoped namespaces.

## Linux build specifics

Build host: Docker Desktop (on Windows) or a Linux host with clang/gcc,
cmake, and `dpkg-dev` / `rpm-build`. Supported targets: Ubuntu 16.04+,
Debian 9+, RHEL/CentOS 7+, Amazon Linux 2, any systemd-based distro;
architectures x64 and arm64. RPMs are GPG-signed; the public key ships
with the repo config.

## CI / release

CI runs in GitHub Actions — see [../.github/workflows/](../.github/workflows/),
primarily `all_solutions.yml` for build + unit/integration tests on every
PR. Releases are driven by **release-please**: conventional commits bump
the version, regenerate `CHANGELOG.md`, open a release PR; merging tags
and publishes artifacts.

## Troubleshooting

**Profiler build fails:**
- C++ ATL for the current VS build tools installed (x86 *and* x64)?
- Docker Desktop running (required for the Linux profiler build)?
- Did you open `Profiler.sln` with the latest Visual Studio?

**MSI build fails:**
- HeatWave (WiX) VS extension installed?
- WiX toolset version matches what `MsiInstaller.sln` expects?
- All upstream agent-home outputs exist (build `FullAgent.sln` first)?

**NuGet pack errors:**
- Clear the NuGet cache: `dotnet nuget locals all --clear`.
- Verify package references resolved in `FullAgent.sln`.
- Connectivity to nuget.org / internal feeds.

**Linux package build fails:**
- Docker Desktop running with enough disk and memory?
- Scripts under `build/Linux/build/` have execute permissions
  (`chmod +x` if cloned on Windows and mounted into Linux containers)?
- Conflicting prior container state — `docker compose down -v` to reset.

## Code signing

Windows profiler DLL and MSI are Authenticode-signed; Linux RPMs are
GPG-signed. Production keys live in the secret vault and are only
available to CI. `build/keys/` is present for local work but its contents
are gitignored.
