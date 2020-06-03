####################################################################################
# build_home - Builds a set of home folders to test and run the agent on a dev box #
####################################################################################

Param(
    [ValidateSet("Debug","Release")][string]$Configuration = "Debug",
    [ValidateSet("All", "Windows", "Linux","Framework", "CoreAll","CoreWindows","CoreLinux")][string]$Type = "All",
    [ValidateSet("All","x64","x86")][string]$Architecture = "All",
    [Parameter(Mandatory=$true)][string]$HomePath,
    [string]$gpgKeyPath = "",
    [switch]$BuildHomeOnly = $false,
    [switch]$KeepNewRelicConfig = $false,
    [switch]$SetSystemEnvironment = $false,
    [switch]$SetSessionEnvironment = $false
)

$ErrorActionPreference = "Stop"

##############
# Validation #
##############

# Setup the HomePath if it doesn't exist
if ( -Not (Test-Path $HomePath )) {
    New-Item -Path $HomePath -ItemType Directory -Force
}

# Linux cannot be x86
if (($Type -like "Linux" -or $Type -like "All" -or $Type -like "CoreAll" -or $Type -like "CoreLinux") -and $Architecture -like "x86") {
    Write-Host "Linux does not support x86, setting up x64 architecture for Linux only."
}

####################################
# Setup and resolve pathing Part 1 #
####################################

$rootDirectory = Resolve-Path "$(Split-Path -Parent $PSCommandPath)\.."
$HomePath = "$HomePath".TrimEnd("\")

######################
# Build all projects #
######################

if (-Not $BuildHomeOnly) {
    . $rootDirectory\Build\build.ps1 -Configuration $Configuration -gpgKeyPath $gpgKeyPath

    if ($LastExitCode -ne 0) {
        exit $LastExitCode
    }
}

####################################
# Setup and resolve pathing Part 2 #
####################################

$stagingPath = Resolve-Path "$rootDirectory\Build\_staging"
$frameworkBasePath = "$stagingPath\ZipArchiveFramework"
$coreBasePath = "$stagingPath\ZipArchiveCore"

#######################
# Create home folders #
#######################

if ($Type -like "All" -or $Type -like "Windows" -or $Type -like "Framework") {
    if ($Architecture -like "All" -or $Architecture -like "x64") {
        robocopy "$frameworkBasePath-x64\" "$HomePath\newrelichome_x64" /e /xf newrelic.config

        if (-Not $KeepNewRelicConfig) {
            Copy-Item -Force -Verbose "$frameworkBasePath-x64\newrelic.config" -Destination "$HomePath\newrelichome_x64\"
        }
    }

    if ($Architecture -like "All" -or $Architecture -like "x86") {
        robocopy "$frameworkBasePath-x86\" "$HomePath\newrelichome_x86" /e /xf newrelic.config

        if (-Not $KeepNewRelicConfig) {
            Copy-Item -Force -Verbose "$frameworkBasePath-x86\newrelic.config" -Destination "$HomePath\newrelichome_x86\"
        }
    }
}

if ($Type -like "All" -or $Type -like "Windows" -or $Type -like "CoreAll" -or $Type -like "CoreWindows") {
    if ($Architecture -like "All" -or $Architecture -like "x64") {
        robocopy "$coreBasePath-x64\" "$HomePath\newrelichome_x64_coreclr" /e /xf newrelic.config

        if (-Not $KeepNewRelicConfig) {
            Copy-Item -Force -Verbose "$coreBasePath-x64\newrelic.config" -Destination "$HomePath\newrelichome_x64_coreclr\"
        }
    }

    if ($Architecture -like "All" -or $Architecture -like "x86") {
        robocopy "$coreBasePath-x86\" "$HomePath\newrelichome_x86_coreclr" /e /xf newrelic.config

        if (-Not $KeepNewRelicConfig) {
            Copy-Item -Force -Verbose "$coreBasePath-x86\newrelic.config" -Destination "$HomePath\newrelichome_x86_coreclr\"
        }
    }
}

if($Type -like "All" -or $Type -like "Linux" -or $Type -like "CoreAll" -or $Type -like "CoreLinux") {
    robocopy "$coreBasePath-x64\" "$HomePath\newrelichome_x64_coreclr_linux" /e /xf newrelic.config
    Copy-item -Force -Verbose "$stagingPath\NugetAgent\contentFiles\any\netstandard2.0\newrelic\*.so" -Destination "$HomePath\newrelichome_x64_coreclr_linux\"

    if (-Not $KeepNewRelicConfig) {
        Copy-Item -Force -Verbose "$coreBasePath-x64\newrelic.config" -Destination "$HomePath\newrelichome_x64_coreclr_linux\"
    }
}

##################
# Setup env vars #
##################

if ($SetSystemEnvironment) {
    Write-Host "Setting up system environment variables for the agent."
    if ($Type -like "All" -or $Type -like "Windows" -or $Type -like "Framework") {
        [Environment]::SetEnvironmentVariable("COR_ENABLE_PROFILING", "1", "Machine")
        [Environment]::SetEnvironmentVariable("COR_PROFILER", "{71DA0A04-7777-4EC6-9643-7D28B46A8A41}", "Machine")
        if($Architecture -like "All" -or $Architecture -like "x64") {
            [Environment]::SetEnvironmentVariable("COR_PROFILER_PATH", "$HomePath\newrelichome_x64\NewRelic.Profiler.dll", "Machine")
            [Environment]::SetEnvironmentVariable("NEWRELIC_HOME", "$HomePath\newrelichome_x64", "Machine")
        }
        else {
            [Environment]::SetEnvironmentVariable("COR_PROFILER_PATH", "$HomePath\newrelichome_x86\NewRelic.Profiler.dll", "Machine")
            [Environment]::SetEnvironmentVariable("NEWRELIC_HOME", "$HomePath\newrelichome_x86", "Machine")
        }
    }

    if ($Type -like "All" -or $Type -like "Windows" -or $Type -like "CoreAll" -or $Type -like "CoreWindows") {
        [Environment]::SetEnvironmentVariable("CORECLR_ENABLE_PROFILING", "1", "Machine")
        [Environment]::SetEnvironmentVariable("CORECLR_PROFILER", "{36032161-FFC0-4B61-B559-F6C5D41BAE5A}", "Machine")
        if($Architecture -like "All" -or $Architecture -like "x64") {
            [Environment]::SetEnvironmentVariable("CORECLR_PROFILER_PATH", "$HomePath\newrelichome_x64_coreclr\NewRelic.Profiler.dll", "Machine")
            [Environment]::SetEnvironmentVariable("NEWRELIC_HOME", "$HomePath\newrelichome_x64", "Machine")
        }
        else {
            [Environment]::SetEnvironmentVariable("CORECLR_PROFILER_PATH", "$HomePath\newrelichome_x86_coreclr\NewRelic.Profiler.dll", "Machine")
            [Environment]::SetEnvironmentVariable("NEWRELIC_HOME", "$HomePath\newrelichome_x86", "Machine")
        }
    }
}

if ($SetSessionEnvironment) {
    Write-Host "Setting up session environment variables for the agent."
    if ($Type -like "All" -or $Type -like "Windows" -or $Type -like "Framework") {
        $env:COR_ENABLE_PROFILING = 1
        $env:COR_PROFILER =  "{71DA0A04-7777-4EC6-9643-7D28B46A8A41}"
        if($Architecture -like "All" -or $Architecture -like "x64") {
            $env:COR_PROFILER_PATH = "$HomePath\newrelichome_x64\NewRelic.Profiler.dll"
            $env:NEWRELIC_HOME = "$HomePath\newrelichome_x64"
        }
        else {
            $env:COR_PROFILER_PATH = "$HomePath\newrelichome_x86\NewRelic.Profiler.dll"
            $env:NEWRELIC_HOME = "$HomePath\newrelichome_x86"
        }
    }

    if ($Type -like "All" -or $Type -like "Windows" -or $Type -like "CoreAll" -or $Type -like "CoreWindows") {
        $env:CORECLR_ENABLE_PROFILING = 1
        $env:CORECLR_PROFILER = "{36032161-FFC0-4B61-B559-F6C5D41BAE5A}"
        if($Architecture -like "All" -or $Architecture -like "x64") {
            $env:CORECLR_PROFILER_PATH = "$HomePath\newrelichome_x64_coreclr\NewRelic.Profiler.dll"
            $env:NEWRELIC_HOME = "$HomePath\newrelichome_x64"
        }
        else {
            $env:CORECLR_PROFILER_PATH = "$HomePath\newrelichome_x86_coreclr\NewRelic.Profiler.dll"
            $env:NEWRELIC_HOME = "$HomePath\newrelichome_x86"
        }
    }
}
