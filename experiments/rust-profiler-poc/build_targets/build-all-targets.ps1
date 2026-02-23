# Cross-platform build script for New Relic Profiler POC
# Builds for all supported platforms: Windows x86/x64, Linux x64/ARM64, macOS ARM64

param(
    [switch]$Release,
    [string[]]$Targets = @(),
    [switch]$Verbose
)

$ErrorActionPreference = "Stop"

# All supported targets
$AllTargets = @(
    @{ Name = "windows-x86"; Target = "i686-pc-windows-msvc"; Extension = ".dll" },
    @{ Name = "windows-x64"; Target = "x86_64-pc-windows-msvc"; Extension = ".dll" },
    @{ Name = "linux-x64"; Target = "x86_64-unknown-linux-gnu"; Extension = ".so" },
    @{ Name = "linux-arm64"; Target = "aarch64-unknown-linux-gnu"; Extension = ".so" },
    @{ Name = "linux-x64-musl"; Target = "x86_64-unknown-linux-musl"; Extension = ".so"; Priority = "High" },
    @{ Name = "linux-arm64-musl"; Target = "aarch64-unknown-linux-musl"; Extension = ".so"; Priority = "High" },
    @{ Name = "macos-arm64"; Target = "aarch64-apple-darwin"; Extension = ".dylib" }
)

# Use specified targets or all targets
$BuildTargets = if ($Targets.Length -gt 0) {
    $AllTargets | Where-Object { $Targets -contains $_.Name }
} else {
    $AllTargets
}

Write-Host "New Relic Profiler POC - Cross-Platform Build" -ForegroundColor Green
Write-Host "================================================" -ForegroundColor Green
Write-Host "KEY VALUE: musl-based Linux support (Alpine, etc.)" -ForegroundColor Yellow
Write-Host "Current C++ profiler cannot build for musl distributions" -ForegroundColor Yellow
Write-Host ""

$BuildMode = if ($Release) { "release" } else { "debug" }
Write-Host "Build mode: $BuildMode" -ForegroundColor Yellow
Write-Host "Targets: $($BuildTargets.Name -join ', ')" -ForegroundColor Yellow
Write-Host ""

# Create output directory
$OutputDir = ".\target\cross-platform-builds\$BuildMode"
New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null

$TotalTargets = $BuildTargets.Length
$CurrentTarget = 0
$SuccessfulBuilds = @()
$FailedBuilds = @()

foreach ($TargetInfo in $BuildTargets) {
    $CurrentTarget++
    $Target = $TargetInfo.Target
    $Name = $TargetInfo.Name
    $Extension = $TargetInfo.Extension

    Write-Host "[$CurrentTarget/$TotalTargets] Building for $Name ($Target)..." -ForegroundColor Cyan

    try {
        # Install target if not already installed
        Write-Host "  Ensuring target $Target is installed..." -ForegroundColor Gray
        rustup target add $Target

        # Build command
        $BuildArgs = @("build", "--target", $Target)
        if ($Release) {
            $BuildArgs += "--release"
        }
        if ($Verbose) {
            $BuildArgs += "--verbose"
        }

        # Execute build
        $Output = & cargo @BuildArgs 2>&1
        if ($LASTEXITCODE -ne 0) {
            throw "Build failed with exit code $LASTEXITCODE"
        }

        # Copy output to organized directory
        $SourcePath = ".\target\$Target\$BuildMode\newrelic_profiler_poc$Extension"
        $DestPath = "$OutputDir\newrelic_profiler_$Name$Extension"

        if (Test-Path $SourcePath) {
            Copy-Item $SourcePath $DestPath -Force
            $FileSize = (Get-Item $DestPath).Length
            Write-Host "  ✓ Built successfully: $DestPath ($([math]::Round($FileSize/1024, 1)) KB)" -ForegroundColor Green
            $SuccessfulBuilds += $Name
        } else {
            throw "Output file not found: $SourcePath"
        }

    } catch {
        Write-Host "  ✗ Build failed: $_" -ForegroundColor Red
        if ($Verbose -and $Output) {
            Write-Host "Build output:" -ForegroundColor Gray
            $Output | Write-Host -ForegroundColor DarkGray
        }
        $FailedBuilds += $Name
    }

    Write-Host ""
}

# Summary
Write-Host "Build Summary" -ForegroundColor Green
Write-Host "=============" -ForegroundColor Green
Write-Host "Successful builds: $($SuccessfulBuilds.Length)/$TotalTargets" -ForegroundColor Green
if ($SuccessfulBuilds.Length -gt 0) {
    Write-Host "  - $($SuccessfulBuilds -join ', ')" -ForegroundColor Green
}

if ($FailedBuilds.Length -gt 0) {
    Write-Host "Failed builds: $($FailedBuilds.Length)" -ForegroundColor Red
    Write-Host "  - $($FailedBuilds -join ', ')" -ForegroundColor Red
}

Write-Host ""
Write-Host "Output directory: $OutputDir" -ForegroundColor Yellow

# List all built files
if (Test-Path $OutputDir) {
    Write-Host "Built files:" -ForegroundColor Yellow
    Get-ChildItem $OutputDir -File | ForEach-Object {
        $Size = [math]::Round($_.Length/1024, 1)
        Write-Host "  - $($_.Name) ($Size KB)" -ForegroundColor Gray
    }
}

if ($FailedBuilds.Length -gt 0) {
    exit 1
}