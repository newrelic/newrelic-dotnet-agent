$ErrorActionPreference = "Stop"

function ExitIfFailLastExitCode {
    if ($LastExitCode -ne 0) {
        exit $LastExitCode
    }
}

if(!(Test-Path -Path .\TestResults )){
    New-Item -ItemType directory -Path .\TestResults
}

$rootRepositoryPath = (Get-Item (Split-Path $script:MyInvocation.MyCommand.Path)).parent.parent.FullName
$nUnitConsolePath = Join-Path $rootRepositoryPath "Build\Tools\NUnit.Console-3.9.0\nunit3-console.exe"

Write-Host "Executing NUnit Tests"
$fileNames = (Get-ChildItem -Recurse -Path 'Tests\UnitTests' -Include @('*Tests.dll', '*Test.dll', '*Test.Legacy.dll') | Where { !$_.FullName.Contains('obj\Release')} | Select Name -Unique)
$files = (Get-ChildItem -Recurse -Path 'Tests\UnitTests' -Include @('*Tests.dll', '*Test.dll', '*Test.Legacy.dll') | Where { !$_.FullName.Contains('obj\Release')})
$unitTestPaths = @()
for ($i = 0; $i -lt $fileNames.Length; $i++) {
    $files | % { if ($_.Name -eq $fileNames[$i].Name) { $unitTestPaths += $_.FullName; Continue } }
}
& $nUnitConsolePath $unitTestPaths --result="TestResults\NUnit2-results.xml;format=nunit2"
ExitIfFailLastExitCode

Write-Host "Executing 64-bit Profiled Methods Tests"
$x64TestPath = Join-Path $rootRepositoryPath "Agent\NewRelic\Profiler\ProfiledMethods\bin\Release\x64"
$env:COR_ENABLE_PROFILING=1
$env:COR_PROFILER="{71DA0A04-7777-4EC6-9643-7D28B46A8A41}"
$env:COR_PROFILER_PATH="$x64TestPath\TestNewRelicHome\NewRelic.Profiler.dll"
$env:NEWRELIC_HOME="$x64TestPath\TestNewRelicHome"
$env:NEWRELIC_PROFILER_LOG_DIRECTORY="$x64TestPath\Logs"
& $nUnitConsolePath "$x64TestPath\ProfiledMethods.dll" --result="TestResults\TestResults64.xml;format=nunit2"
ExitIfFailLastExitCode

Write-Host "Executing 32-bit Profiled Methods Tests"
$x32TestPath = Join-Path $rootRepositoryPath "Agent\NewRelic\Profiler\ProfiledMethods\bin\Release\x86"
$env:COR_ENABLE_PROFILING=1
$env:COR_PROFILER="{71DA0A04-7777-4EC6-9643-7D28B46A8A41}"
$env:COR_PROFILER_PATH="$x32TestPath\TestNewRelicHome\NewRelic.Profiler.dll"
$env:NEWRELIC_HOME="$x32TestPath\TestNewRelicHome\"
$env:NEWRELIC_PROFILER_LOG_DIRECTORY="$x32TestPath\Logs"
& $nUnitConsolePath "$x32TestPath\ProfiledMethods.dll" --x86 --result="TestResults\TestResults32.xml;format=nunit2"
ExitIfFailLastExitCode

Write-Host "Executing Linux Profiler Tests"
& .\Agent\NewRelic\Profiler\build\scripts\run_linux_tests.ps1
ExitIfFailLastExitCode
