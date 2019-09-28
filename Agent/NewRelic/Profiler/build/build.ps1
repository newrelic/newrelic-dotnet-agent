param(
    [ValidateNotNullOrEmpty()]
    [ValidateSet('all','linux','windows','x64','x86')]
    [string]$Platform="all",

    [ValidateNotNullOrEmpty()]
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration="Release"
)

function ExitIfFailLastExitCode {
    if ($LastExitCode -ne 0) {
        exit $LastExitCode
    }
}

$vsRootPath = "C:\Program Files (x86)\Microsoft Visual Studio"
if (!(Test-Path $vsRootPath)) { Write-Error "Unable to locate Visual Studio install at: $vsRootPath"; exit 1; }

$vsVersion = ""
$supportedVersions = @("2017")
foreach ($version in $supportedVersions) {
    if ((Test-Path "$vsRootPath\$version")) { $vsVersion = $version; break; }
}
if ($vsVersion -eq "") { Write-Error "Unable to locate supported version of Visual Studio: $($supportedVersions -join ' or ')"; exit 1; }

$vsEdition = ""
$supportedEditions = @("BuildTools","Enterprise")
foreach ($edition in $supportedEditions) {
    if ((Test-Path "$vsRootPath\$vsVersion\$edition")) { $vsEdition = $edition; break; }
}
if ($vsEdition -eq "") { Write-Error "Unable to locate supported edition of Visual Studio: $($supportedEditions -join ' or ')"; exit 1; }

$vsPath = "$vsRootPath\$vsVersion\$vsEdition"
$msBuildPath = "$vsPath\MSBuild\15.0\Bin\MSBuild.exe"
$msBuildPathx64 = "$vsPath\MSBuild\15.0\Bin\amd64\MSBuild.exe"
if (!(Test-Path $msBuildPath)) { Write-Error "Unable to locate x86 MSBuild.exe at: $msBuildPath"; exit 1; }
if (!(Test-Path $msBuildPathx64)) { Write-Error "Unable to locate x64 MSBuild.exe at: $msBuildPathx64 "; exit 1; }

Write-Host "Using Visual Studio tools located at: $vsPath"
Write-Host "Building Platform=$Platform and Configuration=$Configuration"

$repoRoot = (Get-Item $PSCommandPath).Directory.Parent.Parent.Parent.Parent.FullName
$nugetPath = "$repoRoot\Build\Tools\nuget.exe"
$profilerRoot = "$repoRoot\Agent\NewRelic\Profiler"
$profilerSolutionPath = "$profilerRoot\NewRelic.Profiler.sln"
$outputPath = "$repoRoot\Agent\_profilerBuild"
$linuxOutputPath = "$outputPath\linux-release"

$restoreNuget = $Platform -eq "all" -or $Platform -eq "windows" -or $Platform -eq "x64" -or $Platform -eq "x86"
$buildx64 = $Platform -eq "all" -or $Platform -eq "windows" -or $Platform -eq "x64"
$buildx86 = $Platform -eq "all" -or $Platform -eq "windows" -or $Platform -eq "x86"
$buildLinux = $Platform -eq "all" -or $Platform -eq "linux"

if ($Platform -eq "all") {
    if (Test-Path $outputPath) { Remove-Item $outputPath -Recurse }
    if (Test-Path $outputPath) { Write-Error "Ouput path not cleared out: $outputPath"; exit 1; }
}

if ($restoreNuget) {
    Write-Host "-- Profiler build: Restoring NuGet packages"
    & $nugetPath restore $profilerSolutionPath -Source "https://www.nuget.org/api/v2"
    ExitIfFailLastExitCode
}

if ($buildx64) {
    Write-Host "-- Profiler build: x64-$Configuration"
    & $msBuildPathx64 /p:Platform=x64 /p:Configuration=$Configuration $profilerSolutionPath
    ExitIfFailLastExitCode
}

if ($buildx86) {
    Write-Host "-- Profiler build: x86-$Configuration"
    & "$msBuildPath" /p:Platform=Win32 /p:Configuration=$Configuration $profilerSolutionPath
    ExitIfFailLastExitCode
}

if ($buildLinux) {
    Write-Host "-- Profiler build: linux-release"

    if ($Configuration -eq "Debug") {
        Write-Host "Configuration=Debug is not currently supported by this script when building the linux profiler. Building Configuration=Release instead."
    }

    & $profilerRoot\build\scripts\build_linux.ps1
    ExitIfFailLastExitCode

    if (!(Test-Path $linuxOutputPath)) { New-Item $linuxOutputPath -ItemType Directory }
    Move-Item "$profilerRoot\libNewRelicProfiler.so" "$linuxOutputPath"
}