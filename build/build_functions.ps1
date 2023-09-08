############################################################
# Copyright 2020 New Relic Corporation. All rights reserved.
# SPDX-License-Identifier: Apache-2.0
############################################################

###################
# Build Functions #
###################

function Get-HomeRootPath {
    param(
        [string]$Path
    )

    $ErrorActionPreference = "Stop"

    if (-Not $Path) {
        $Path = Resolve-Path "$(Split-Path -Parent $PSCommandPath)\..\src\Agent"
    }
  
    if ( -Not (Test-Path $Path )) {
        New-Item -Path $Path -ItemType Directory -Force
    }

    $Path.TrimEnd("\")
}
function New-HomeStructure {
    param(
        [Parameter(Mandatory=$true)][string]$Path,
        [Parameter(Mandatory=$true)][string]$Name
    )

    $ErrorActionPreference = "Stop"

    if (-Not (Resolve-Path "$Path")) {
        return
    }

    $homeFolderPath = New-item -Path "$Path" -Name "$Name" -Type Directory -Force 
    New-item -Path "$homeFolderPath" -Name "extensions" -Type Directory -Force
}

function Copy-ExtensionsInstrumentation {
    param(
        [Parameter(Mandatory=$true)][string]$Destination,
        [Parameter(Mandatory=$true)][hashtable]$Extensions
    )

    $ErrorActionPreference = "Stop"

    if (-Not (Resolve-Path "$Destination")) {
        Write-Host "Could not resolve destination."
        return
    }

    if ($Extensions.Keys.Count -eq 0) {
        Write-Host "Extensions map was empty."
        return
    }

    foreach ($item in $Extensions.GetEnumerator()) {
        $dllPath = $item.Key.FullName
        $xmlPath = $item.Value.FullName
        $xmlTargetName = $item.Key.BaseName

        Copy-Item -Path "$dllPath" -Destination "$Destination\extensions" -Force 
        Copy-Item -Path "$xmlPath" -Destination "$Destination\extensions\$xmlTargetName.Instrumentation.xml" -Force 
    }
}

function Copy-ExtensionsStorage {
    param(
        [Parameter(Mandatory=$true)][string]$Destination,
        [Parameter(Mandatory=$true)][array]$Extensions
    )

    $ErrorActionPreference = "Stop"

    if (-Not (Resolve-Path "$Destination")) {
        Write-Host "Could not resolve destination."
        return
    }

    if ($Extensions.Count -eq 0) {
        Write-Host "Extensions map was empty."
        return
    }

    foreach ($item in $Extensions) {
        $dllPath = $item.FullName
        Copy-Item -Path "$dllPath" -Destination "$Destination\extensions" -Force 
    }
}

function Copy-ExtensionsOther {
    param(
        [Parameter(Mandatory=$true)][string]$RootDirectory,
        [Parameter(Mandatory=$true)][string]$Destination,
        [Parameter(Mandatory=$true)][ValidateSet("Debug","Release")][string]$Configuration,
        [Parameter(Mandatory=$true)][ValidateSet("Framework","Core")][string]$Type
    )

    $ErrorActionPreference = "Stop"
    
    if (-Not (Resolve-Path "$RootDirectory")) {
        Write-Host "Could not resolve root directory."
        return
    }

    if (-Not (Resolve-Path "$Destination")) {
        Write-Host "Could not resolve destination."
        return
    }

    Copy-Item -Path "$RootDirectory\src\Agent\Miscellaneous\NewRelic.Providers.Wrapper.Misc.Instrumentation.xml" -Destination "$Destination\extensions" -Force 
    Copy-Item -Path "$RootDirectory\src\Agent\NewRelic\Agent\Core\Extension\extension.xsd" -Destination "$Destination\extensions" -Force 

    if ($Type -like "Framework"){
        Copy-Item -Path "$RootDirectory\src\_build\AnyCPU-$Configuration\NewRelic.Core\net462-ILRepacked\NewRelic.Core.dll" -Destination "$Destination\extensions" -Force 
        Copy-Item -Path "$RootDirectory\src\Agent\NewRelic\Agent\Parsing\bin\$Configuration\net462\NewRelic.Parsing.dll" -Destination "$Destination\extensions" -Force 
    }

    if ($Type -like "Core"){
        Copy-Item -Path "$RootDirectory\src\_build\AnyCPU-$Configuration\NewRelic.Core\netstandard2.0-ILRepacked\NewRelic.Core.dll" -Destination "$Destination\extensions" -Force 
        Copy-Item -Path "$RootDirectory\src\Agent\NewRelic\Agent\Parsing\bin\$Configuration\netstandard2.0\NewRelic.Parsing.dll" -Destination "$Destination\extensions" -Force 
    }
}

function Copy-AgentRoot {
    param(
        [Parameter(Mandatory=$true)][string]$RootDirectory,
        [Parameter(Mandatory=$true)][string]$Destination,
        [Parameter(Mandatory=$true)][ValidateSet("Debug","Release")][string]$Configuration,
        [Parameter(Mandatory=$true)][ValidateSet("Framework","Core")][string]$Type,
        [Parameter(Mandatory=$true)][ValidateSet("x64","x86","ARM64")][string]$Architecture,
        [switch]$Linux
    )

    $ErrorActionPreference = "Stop"

    Copy-Item -Path "$RootDirectory\licenses\LICENSE.txt" -Destination "$Destination" -Force 
    Copy-Item -Path "$RootDirectory\licenses\THIRD_PARTY_NOTICES.txt" -Destination "$Destination" -Force 
    Copy-Item -Path "$RootDirectory\src\Agent\NewRelic\Agent\Core\Config\Configuration.xsd" -Destination "$Destination\newrelic.xsd" -Force 
    
    if ($Type -like "Framework") {
        Copy-Item -Path "$RootDirectory\src\Agent\NewRelic\Agent\Extensions\NewRelic.Agent.Extensions\bin\$Configuration\net462\NewRelic.Agent.Extensions.dll" -Destination "$Destination" -Force 
        Copy-Item -Path "$RootDirectory\src\_build\AnyCPU-$Configuration\NewRelic.Agent.Core\net462-ILRepacked\NewRelic.Agent.Core.dll" -Destination "$Destination" -Force 
    }

    if ($Type -like "Core") {
        Copy-Item -Path "$RootDirectory\src\Agent\NewRelic\Agent\Extensions\NewRelic.Agent.Extensions\bin\$Configuration\netstandard2.0\NewRelic.Agent.Extensions.dll" -Destination "$Destination" -Force 
        Copy-Item -Path "$RootDirectory\src\_build\AnyCPU-$Configuration\NewRelic.Agent.Core\netstandard2.0-ILRepacked\NewRelic.Agent.Core.dll" -Destination "$Destination" -Force 
        Copy-Item -Path "$RootDirectory\src\Agent\Miscellaneous\core-agent-readme.md" -Destination "$Destination\README.md" -Force 
    }

    
    if ($Linux) {
        if ($Architecture -like "x64") {
            Copy-Item -Path "$RootDirectory\src\Agent\NewRelic\Home\bin\$Configuration\netstandard2.0\profiler\linux_x64\libNewRelicProfiler.so" -Destination "$Destination" -Force 
        }
        if ($Architecture -like "ARM64") {
            Copy-Item -Path "$RootDirectory\src\Agent\NewRelic\Home\bin\$Configuration\netstandard2.0\profiler\linux_arm64\libNewRelicProfiler.so" -Destination "$Destination" -Force 
        }
    }
    else {
        if ($Type -like "Framework") {
            $grpcDir = Get-GrpcPackagePath $RootDirectory
            Copy-Item -Path "$grpcDir\runtimes\win-x86\native\*.dll" -Destination "$Destination" -Force
            Copy-Item -Path "$grpcDir\runtimes\win-x64\native\*.dll" -Destination "$Destination" -Force
        }

        if ($Architecture -like "x64" ) {
            Copy-Item -Path "$RootDirectory\src\Agent\NewRelic\Home\bin\$Configuration\netstandard2.0\profiler\x64\NewRelic.Profiler.dll" -Destination "$Destination" -Force 
        }
        else {
            Copy-Item -Path "$RootDirectory\src\Agent\NewRelic\Home\bin\$Configuration\netstandard2.0\profiler\x86\NewRelic.Profiler.dll" -Destination "$Destination" -Force 
        }
    }

}

function Copy-AgentApi {
    param(
        [Parameter(Mandatory=$true)][string]$RootDirectory,
        [Parameter(Mandatory=$true)][string]$Destination,
        [Parameter(Mandatory=$true)][ValidateSet("Debug","Release")][string]$Configuration,
        [Parameter(Mandatory=$true)][ValidateSet("Framework","Core")][string]$Type
    )

    $ErrorActionPreference = "Stop"

    if ($Type -like "Framework") {
        Copy-Item -Path "$RootDirectory\src\_build\AnyCPU-$Configuration\NewRelic.Api.Agent\net462\NewRelic.Api.Agent.dll" -Destination "$Destination" -Force 
    }

    if ($Type -like "Core") {
        Copy-Item -Path "$RootDirectory\src\_build\AnyCPU-$Configuration\NewRelic.Api.Agent\netstandard2.0\NewRelic.Api.Agent.dll" -Destination "$Destination" -Force 
    }
}

function Copy-NewRelicConfig {
    param(
        [Parameter(Mandatory=$true)][string]$RootDirectory,
        [Parameter(Mandatory=$true)][string]$Destination
    )

    $ErrorActionPreference = "Stop"

    Copy-Item -Path "$RootDirectory\src\Agent\Configuration\newrelic.config" -Destination "$Destination" -Force 
}

function Set-SystemEnvironment {
    Param(
        [ValidateSet("All", "Windows", "Linux","Framework", "CoreAll","CoreWindows","CoreLinux")][string]$Type = "All",
        [ValidateSet("All","x64","x86")][string]$Architecture = "All",
        [Parameter(Mandatory=$true)][string]$HomePath
    )

    $ErrorActionPreference = "Stop"

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

function Set-SessionEnvironment {
    Param(
        [ValidateSet("All", "Windows", "Linux","Framework", "CoreAll","CoreWindows","CoreLinux")][string]$Type = "All",
        [ValidateSet("All","x64","x86")][string]$Architecture = "All",
        [Parameter(Mandatory=$true)][string]$HomePath
    )

    $ErrorActionPreference = "Stop"

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

#####################
# Private Functions #
#####################

function Get-GrpcPackagePath {
    param(
        [Parameter(Mandatory=$true)][string]$RootDirectory
    )

    $ErrorActionPreference = "Stop"

    $match = Select-String -Path "$RootDirectory\src\Agent\NewRelic\Agent\Core\Core.csproj" -Pattern '<PackageReference Include="Grpc.Core"'
    $versionIndex = $match.Line.IndexOf('"', $match.Line.IndexOf('Version')) + 1
    $grpcVersion = $match.Line.TrimEnd('/>').TrimEnd(' ').TrimEnd('"').Substring($versionIndex)
    $rawpkgList = $(dotnet nuget locals global-packages --list)
    $pkgList = $rawpkgList -Replace "global-packages: "
    $grpcDir = "$pkgList\Grpc.Core\$grpcVersion"
    $grpcDir
}
