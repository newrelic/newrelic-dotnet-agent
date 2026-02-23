# Docker-based cross-platform build script for New Relic Profiler POC
# This proves comprehensive cross-platform support including musl (Alpine Linux)

param(
    [string[]]$Targets = @("alpine", "ubuntu", "alpine-arm64"),
    [switch]$Verbose,
    [switch]$KeepContainers
)

$ErrorActionPreference = "Stop"

# Build configuration
$AllTargets = @{
    "alpine" = @{
        Name = "Alpine Linux x64 (musl)"
        Dockerfile = "alpine-builder.dockerfile"
        Platform = "linux/amd64"
        KeyValue = "Musl support - C++ profiler cannot do this!"
    }
    "ubuntu" = @{
        Name = "Ubuntu Linux x64 (glibc)"
        Dockerfile = "ubuntu-builder.dockerfile"
        Platform = "linux/amd64"
        KeyValue = "Standard Linux compatibility"
    }
    "alpine-arm64" = @{
        Name = "Alpine Linux ARM64 (musl)"
        Dockerfile = "alpine-arm64-builder.dockerfile"
        Platform = "linux/arm64"
        KeyValue = "ARM64 + musl combo - ultimate deployment flexibility!"
    }
}

Write-Host "🦀 New Relic Profiler POC - Docker Cross-Platform Build" -ForegroundColor Green
Write-Host "=======================================================" -ForegroundColor Green
Write-Host "🎯 PROVING: Rust can build for musl targets (C++ cannot!)" -ForegroundColor Yellow
Write-Host ""

# Verify Docker is available
try {
    $dockerVersion = docker --version
    Write-Host "✅ Docker available: $dockerVersion" -ForegroundColor Green
} catch {
    Write-Host "❌ Docker not available. Please install Docker Desktop." -ForegroundColor Red
    exit 1
}

# Check if buildx is available for multi-platform
try {
    docker buildx version | Out-Null
    Write-Host "✅ Docker Buildx available for multi-platform builds" -ForegroundColor Green
} catch {
    Write-Host "⚠️  Docker Buildx not available - ARM64 builds may not work" -ForegroundColor Yellow
}

Write-Host ""

$BuildResults = @()
$SuccessfulBuilds = @()
$FailedBuilds = @()

foreach ($targetName in $Targets) {
    if (-not $AllTargets.ContainsKey($targetName)) {
        Write-Host "❌ Unknown target: $targetName" -ForegroundColor Red
        continue
    }

    $target = $AllTargets[$targetName]
    Write-Host "🔨 Building: $($target.Name)" -ForegroundColor Cyan
    Write-Host "   Key Value: $($target.KeyValue)" -ForegroundColor Yellow
    Write-Host "   Platform: $($target.Platform)" -ForegroundColor Gray

    try {
        $imageName = "newrelic-profiler-poc-$targetName"
        $containerName = "newrelic-profiler-build-$targetName"

        # Build the Docker image
        Write-Host "   📦 Building Docker image..." -ForegroundColor Gray
        $buildArgs = @(
            "build",
            "--platform", $target.Platform,
            "-f", "docker/$($target.Dockerfile)",
            "-t", $imageName,
            "."
        )

        if ($Verbose) {
            & docker @buildArgs
        } else {
            & docker @buildArgs 2>&1 | Out-Null
        }

        if ($LASTEXITCODE -ne 0) {
            throw "Docker image build failed"
        }

        # Run the build in container
        Write-Host "   🏗️  Running build in container..." -ForegroundColor Gray
        $runArgs = @(
            "run",
            "--rm",
            "--platform", $target.Platform,
            "--name", $containerName,
            "-v", "${PWD}:/workspace",
            $imageName
        )

        $buildOutput = & docker @runArgs 2>&1
        $buildExitCode = $LASTEXITCODE

        if ($buildExitCode -eq 0) {
            Write-Host "   ✅ SUCCESS!" -ForegroundColor Green
            $SuccessfulBuilds += $target.Name

            # Show key results
            $successLines = $buildOutput | Where-Object { $_ -match "(✅|🎯|📦)" }
            foreach ($line in $successLines) {
                Write-Host "      $line" -ForegroundColor DarkGreen
            }
        } else {
            Write-Host "   ❌ FAILED!" -ForegroundColor Red
            $FailedBuilds += $target.Name

            if ($Verbose) {
                Write-Host "   Build output:" -ForegroundColor Gray
                $buildOutput | ForEach-Object { Write-Host "      $_" -ForegroundColor DarkGray }
            }
        }

        $BuildResults += [PSCustomObject]@{
            Target = $target.Name
            Success = ($buildExitCode -eq 0)
            KeyValue = $target.KeyValue
            Output = $buildOutput
        }

    } catch {
        Write-Host "   ❌ ERROR: $_" -ForegroundColor Red
        $FailedBuilds += $target.Name
    }

    Write-Host ""
}

# Summary Report
Write-Host "🎯 CROSS-PLATFORM BUILD RESULTS" -ForegroundColor Green
Write-Host "================================" -ForegroundColor Green

Write-Host "✅ Successful builds: $($SuccessfulBuilds.Count)/$($Targets.Count)" -ForegroundColor Green
foreach ($success in $SuccessfulBuilds) {
    $target = $AllTargets.Keys | Where-Object { $AllTargets[$_].Name -eq $success } | Select-Object -First 1
    if ($target) {
        Write-Host "   - $success" -ForegroundColor Green
        Write-Host "     🎯 $($AllTargets[$target].KeyValue)" -ForegroundColor Yellow
    }
}

if ($FailedBuilds.Count -gt 0) {
    Write-Host ""
    Write-Host "❌ Failed builds: $($FailedBuilds.Count)" -ForegroundColor Red
    foreach ($failure in $FailedBuilds) {
        Write-Host "   - $failure" -ForegroundColor Red
    }
}

Write-Host ""
Write-Host "📊 BUSINESS VALUE DEMONSTRATED:" -ForegroundColor Green

if ($SuccessfulBuilds -contains "Alpine Linux x64 (musl)") {
    Write-Host "🎉 MAJOR WIN: musl compilation successful!" -ForegroundColor Green
    Write-Host "   - Alpine Linux containers now supported" -ForegroundColor Yellow
    Write-Host "   - Solves deployment gap C++ profiler has" -ForegroundColor Yellow
}

if ($SuccessfulBuilds -contains "Alpine Linux ARM64 (musl)") {
    Write-Host "🚀 BREAKTHROUGH: ARM64 + musl combination works!" -ForegroundColor Green
    Write-Host "   - AWS Graviton processor support" -ForegroundColor Yellow
    Write-Host "   - Modern ARM64 cloud infrastructure ready" -ForegroundColor Yellow
}

if ($SuccessfulBuilds -contains "Ubuntu Linux x64 (glibc)") {
    Write-Host "✅ COMPATIBILITY: Standard Linux support confirmed" -ForegroundColor Green
}

# Check if we have artifacts
Write-Host ""
Write-Host "🗂️  BUILD ARTIFACTS:" -ForegroundColor Green
$artifactDirs = @(
    "target/x86_64-unknown-linux-musl/release",
    "target/x86_64-unknown-linux-gnu/release",
    "target/aarch64-unknown-linux-musl/release"
)

foreach ($dir in $artifactDirs) {
    if (Test-Path $dir) {
        $soFiles = Get-ChildItem "$dir/*.so" -ErrorAction SilentlyContinue
        if ($soFiles) {
            foreach ($file in $soFiles) {
                $size = [math]::Round($file.Length / 1KB, 1)
                Write-Host "   📦 $($file.Name) ($size KB)" -ForegroundColor Gray
                Write-Host "      Path: $($file.FullName)" -ForegroundColor DarkGray
            }
        }
    }
}

if ($FailedBuilds.Count -eq 0) {
    Write-Host ""
    Write-Host "🎉 COMPLETE SUCCESS: All cross-platform builds work!" -ForegroundColor Green
    Write-Host "Ready to present comprehensive musl/ARM64 support to management." -ForegroundColor Green
    exit 0
} else {
    Write-Host ""
    Write-Host "⚠️  Partial success: Some builds need investigation." -ForegroundColor Yellow
    exit 1
}