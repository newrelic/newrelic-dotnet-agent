# New Relic .NET Agent - Build System

This document describes the build tools, packaging, and release process for the New Relic .NET Agent.

## Overview

The build system is responsible for:
- Building and packaging agent artifacts
- Creating installation packages (MSI, NuGet, etc.)
- Validating releases
- Linux package creation
- Azure Site Extensions
- Release automation

## Directory Structure

```
build/
├── ArtifactBuilder/          # Main build orchestration tool
├── Packaging/                # Package creation scripts
├── Linux/                    # Linux package building
├── Scripts/                  # Build and test scripts
├── NewRelic.NuGetHelper/     # NuGet package utilities
├── NugetValidator/           # Validates NuGet packages
├── NugetVersionDeprecator/   # Deprecates old NuGet versions
├── S3Validator/              # Validates S3 uploads
├── ReleaseNotesBuilder/      # Generates release notes
├── Dotty/                    # .NET SDK management tool
├── keys/                     # Signing keys
└── Tools/                    # Build tools (NUnit, xUnit, etc.)
```

## Build Tools

### 1. ArtifactBuilder (`ArtifactBuilder/`)

The primary build orchestration tool that creates all distributable artifacts.

**Purpose:**
- Coordinates multi-step build process
- Creates platform-specific agent packages
- Builds NuGet packages
- Creates MSI installer
- Generates Azure Site Extensions
- Organizes output artifacts

**Key Responsibilities:**
- Build FullAgent.sln for all configurations
- Copy profiler binaries to agent home directories
- Create NuGet packages for agent, API, and profiler
- Build MSI installer (if HeatWave installed)
- Create download site structure
- Generate Azure Site Extension packages
- Create Linux packages

**Output:**
All artifacts are organized in a structured output directory ready for release.

**Usage:**
Typically invoked by CI/CD pipelines or manually for local testing.

### 2. Dotty (`Dotty/`)

.NET SDK version management tool.

**Purpose:**
Helps manage multiple .NET SDK versions for testing across different frameworks.

**Features:**
- Download specific .NET SDK versions
- Manage multiple SDK installations
- Switch between SDK versions for testing

### 3. NuGet Helper (`NewRelic.NuGetHelper/`)

Utilities for working with NuGet packages.

**Features:**
- Package creation helpers
- Version management
- Dependency resolution
- Package manipulation

### 4. NuGet Validator (`NugetValidator/`)

Validates NuGet packages before release.

**Validation Checks:**
- Package metadata correctness
- File contents verification
- Dependency version validation
- Breaking change detection
- License information
- Documentation presence

### 5. NuGet Version Deprecator (`NugetVersionDeprecator/`)

Manages deprecation of old NuGet package versions.

**Purpose:**
- Mark old versions as deprecated on NuGet.org
- Provide migration guidance
- Automate deprecation process

### 6. S3 Validator (`S3Validator/`)

Validates uploaded artifacts on S3/CDN.

**Validation:**
- Verifies all expected files present
- Checks file integrity
- Validates download URLs
- Ensures proper permissions

### 7. Release Notes Builder (`ReleaseNotesBuilder/`)

Generates release notes from changelog and git history.

**Features:**
- Parses CHANGELOG.md
- Formats for different audiences
- Generates HTML/Markdown output
- Integration with release process

## Packaging

### Package Types

The build system creates multiple package formats:

#### 1. NuGet Packages (`Packaging/NugetAgent/`, etc.)

**Agent Package:**
- Package: `NewRelic.Agent`
- Contains: Full agent for all platforms
- Usage: Install agent via NuGet

**API Package:**
- Package: `NewRelic.Agent.Api`
- Contains: Public API only
- Usage: Compile-time reference for custom instrumentation

**Profiler Package:**
- Package: `NewRelic.Agent.Internal.Profiler`
- Contains: Native profiler binaries
- Usage: Internal dependency of agent package

**Azure Packages:**
- `NewRelic.Azure.WebSites.x64`
- `NewRelic.Azure.WebSites.x86`
- `NewRelic.Azure.WebSites.Extension`
- Platform-specific packages for Azure App Service

**Cloud Services:**
- `NewRelic.Azure.CloudServices`
- Package for Azure Cloud Services (classic)

#### 2. MSI Installer (`../src/Agent/MsiInstaller/`)

**Package:** `NewRelicAgent_x64.msi` / `NewRelicAgent_x86.msi`
**Usage:** Windows installer for IIS and .NET Framework applications

**Features:**
- GUI installation wizard
- IIS integration
- Registry configuration
- Environment variable setup
- Uninstall support

**Building:**
Requires HeatWave extension in Visual Studio. See [../src/Agent/MsiInstaller/](../src/Agent/MsiInstaller/).

#### 3. Download Site (`Packaging/DownloadSite/`)

**Package:** Structured directory for download.newrelic.com
**Contents:**
- Agent home directories (zipped)
- MSI installers
- NuGet packages
- Linux packages
- SHA256 checksums
- README files

**Structure:**
```
newrelic-dotnet-agent_VERSION/
├── newrelic-dotnet-agent_VERSION_amd64.deb
├── newrelic-dotnet-agent-VERSION-1.x86_64.rpm
├── newrelic-dotnet-agent_VERSION_arm64.deb
├── newrelic-dotnet-agent-VERSION-1.aarch64.rpm
├── NewRelicAgent_x64_VERSION.msi
├── NewRelicAgent_x86_VERSION.msi
├── newrelichome_x64_coreclr_VERSION.zip
├── newrelichome_x86_coreclr_VERSION.zip
├── newrelichome_x64_VERSION.zip
├── newrelichome_x86_VERSION.zip
└── SHA256/checksums
```

#### 4. Linux Packages (`Linux/`)

**Packages:**
- `.deb` for Debian/Ubuntu (x64, arm64)
- `.rpm` for RHEL/CentOS (x64, arm64)

**Installation Locations:**
- Agent: `/usr/local/newrelic-dotnet-agent/`
- Configuration: `/usr/local/newrelic-dotnet-agent/newrelic.config`

**Building:**
- Uses Docker containers for clean builds
- Creates packages for multiple architectures
- Signs packages with GPG

**Build Scripts:**
- [Linux/build/build_scripts.py](Linux/build/build_scripts.py) - Main build script
- [Linux/yum/newrelic-dotnet-agent.spec](Linux/yum/newrelic-dotnet-agent.spec) - RPM spec
- [Linux/build/deb/](Linux/build/deb/) - Debian package scripts

#### 5. Azure Site Extension (`Packaging/AzureSiteExtension/`)

**Package:** Azure Site Extension for App Service
**Installation:** Via Azure Portal Extensions tab

**Features:**
- One-click installation in Azure
- Automatic configuration
- Per-app or per-site installation

## Build Workflows

### Local Development Build

1. Open `FullAgent.sln` in Visual Studio
2. Select configuration (Debug/Release)
3. Build solution (Ctrl+Shift+B)
4. Agent home directories created in `src/Agent/newrelichome_*/`
5. Ready for local testing

### Full Release Build

1. **Version Update**
   - Update version in source files
   - Update CHANGELOG.md
   - Commit changes

2. **Build All Configurations**
   - Build FullAgent.sln (Release)
   - Build Profiler.sln (all platforms)
   - Build MsiInstaller.sln

3. **Run ArtifactBuilder**
   - Packages all artifacts
   - Creates NuGet packages
   - Builds Linux packages
   - Creates download site structure

4. **Validation**
   - Run NugetValidator
   - Run S3Validator (after upload)
   - Manual smoke testing

5. **Release**
   - Upload to S3/download site
   - Push to NuGet.org
   - Create GitHub release
   - Update documentation

### CI/CD Pipeline

The project uses GitHub Actions for CI/CD. See [../.github/workflows/](../.github/workflows/).

**Key Workflows:**
- `all_solutions.yml` - Builds and tests all solutions
- Build validation on all PRs
- Automated testing
- Code coverage reporting
- Nightly builds

## Build Scripts

### PowerShell Scripts (`Scripts/`)

**Integration Test Scripts:**
- [run-integration-tests.ps1](Scripts/run-integration-tests.ps1) - Run integration tests
- [run-platform-tests.ps1](Scripts/run-platform-tests.ps1) - Run platform-specific tests

**Usage:**
```powershell
# Run integration tests
.\build\Scripts\run-integration-tests.ps1

# Run platform tests
.\build\Scripts\run-platform-tests.ps1
```

### Python Scripts (`Linux/build/`)

**Linux Package Building:**
- `build_scripts.py` - Main Linux build orchestrator
- Uses Docker for isolated builds
- Creates deb and rpm packages

## Build Configuration Files

### Directory.Build.props

Global MSBuild properties at repository root and various subdirectories.

**Common Settings:**
- Version numbers
- Compiler flags
- Warning levels
- Code analysis rules
- Output paths

**Key Properties:**
```xml
<PropertyGroup>
  <AgentVersion>10.48.1</AgentVersion>
  <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  <LangVersion>latest</LangVersion>
</PropertyGroup>
```

### .editorconfig

Code style and formatting rules.

**Enforces:**
- Indentation style
- Line endings
- Naming conventions
- Code analysis rules
- File-scoped namespaces

## Signing and Security

### Code Signing

**Windows:**
- Profiler DLL signed with Authenticode
- MSI installer signed
- Certificate stored securely

**Linux:**
- RPM packages signed with GPG
- Public key distributed via repo config

### Keys Directory (`keys/`)

Contains signing keys and certificates (not committed to repo).

**Key Management:**
- Production keys stored in secure vault
- CI/CD accesses keys via secrets
- Local development uses test keys

## Platform-Specific Builds

### Windows

**Architectures:**
- x86 (32-bit)
- x64 (64-bit)

**Frameworks:**
- .NET Framework 4.6.2+
- .NET Core 3.1+
- .NET 6+

**Build Requirements:**
- Visual Studio 2022
- Windows SDK
- C++ ATL libraries

### Linux

**Architectures:**
- x64 (amd64)
- arm64 (aarch64)

**Supported Distributions:**
- Ubuntu 16.04+ (.deb)
- Debian 9+ (.deb)
- RHEL/CentOS 7+ (.rpm)
- Amazon Linux 2 (.rpm)
- Any systemd-based distribution

**Build Requirements:**
- Docker Desktop (for building on Windows)
- Linux build environment with:
  - clang/gcc
  - cmake
  - dpkg-dev / rpm-build

## Build Tools Directory (`Tools/`)

Third-party tools used during build:

- `NuGet/` - NuGet command-line tool
- `NUnit-Console/` - NUnit test runner
- `XUnit-Console/` - xUnit test runner
- `xsd2code/` - XML schema code generation

## Versioning

### Version Numbers

Format: `MAJOR.MINOR.PATCH` (Semantic Versioning)

**Example:** `10.48.1`
- Major: Breaking changes
- Minor: New features
- Patch: Bug fixes

### Version Sources

1. **Source of Truth:** Agent assembly version
2. **Propagated To:**
   - NuGet package versions
   - MSI product version
   - Linux package versions
   - File names

### Version Management

- Versions updated via release-please automation
- Based on conventional commits
- CHANGELOG.md auto-generated

## Release Process

### Automated Release (via release-please)

1. **Development:**
   - Developers commit with conventional commit messages
   - Example: `feat: add OpenAI instrumentation`
   - Example: `fix: handle disposed streams`

2. **Release PR Created:**
   - release-please bot creates PR
   - Bumps version based on commits
   - Updates CHANGELOG.md
   - Updates version in source files

3. **Release PR Merged:**
   - Tag created automatically
   - GitHub release created
   - Artifacts built by CI
   - Artifacts uploaded to release

4. **Post-Release:**
   - Packages pushed to NuGet
   - Artifacts uploaded to download site
   - Announcements made

### Manual Release Steps

If manual intervention needed:

1. Update version in code
2. Update CHANGELOG.md
3. Build all artifacts via ArtifactBuilder
4. Validate packages
5. Create Git tag
6. Push to NuGet.org
7. Upload to download.newrelic.com
8. Create GitHub release
9. Announce release

## Troubleshooting Builds

### Common Issues

**Profiler Build Fails:**
- Ensure C++ ATL installed
- Check Visual Studio version
- Docker running for Linux builds

**MSI Build Fails:**
- Install HeatWave extension
- Check WiX toolset version
- Verify all dependencies present

**NuGet Package Errors:**
- Clear NuGet cache: `dotnet nuget locals all --clear`
- Verify package references
- Check NuGet.org connectivity

**Linux Package Build Fails:**
- Docker Desktop running
- Sufficient disk space
- Build scripts have execute permissions

### Build Logs

**Local Builds:**
- Visual Studio Output window
- MSBuild logs in `src/_build/`

**CI Builds:**
- GitHub Actions workflow logs
- Artifact download for failed builds

## Performance Considerations

**Parallel Builds:**
- Use `/m` flag with MSBuild for parallel compilation
- Visual Studio automatically parallelizes

**Incremental Builds:**
- Don't clean unless necessary
- Use Debug configuration for fast iteration
- Release builds slower due to optimizations

**Build Times:**
- Full agent build: ~5-10 minutes
- Profiler build: ~2-5 minutes
- Integration tests: ~30-60 minutes

## Related Documentation

- @../claude.md - Main repository guide
- @../src/claude-source.md - Source code architecture
- @../tests/claude-tests.md - Testing guide
- [Development guide](../docs/development.md)
- [MSI Installer](../src/Agent/MsiInstaller/)
- [Profiler build guide](../src/Agent/NewRelic/Profiler/README.md)
