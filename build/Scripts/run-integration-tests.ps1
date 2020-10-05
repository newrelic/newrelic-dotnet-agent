############################################################
# Copyright 2020 New Relic Corporation. All rights reserved.
# SPDX-License-Identifier: Apache-2.0
############################################################

Param (
    [Parameter(Mandatory=$true)]
    [ValidateSet("integration", "unbounded")]
    [string]$testSuite,
    [string]$xunitParams = "",
    [switch]$saveWorkingFolders = $false,
    [string]$secretsFilePath = "",
    [switch]$startUnboundedServices = $false,
    [int]$unboundedServicesStartDelaySeconds = 600,
    [ValidateSet("linux", "windows")]
    [string]$platform
)

if ($saveWorkingFolders) {
    $env:NR_DOTNET_TEST_SAVE_WORKING_DIRECTORY = 1;
}

$rootDirectory = Resolve-Path "$(Split-Path -Parent $PSCommandPath)\..\.."
$xUnitPath = Resolve-Path "$rootDirectory\build\Tools\XUnit-Console\xunit.console.exe"
$unboundedServicesControlPath = Resolve-Path "$rootDirectory\tests\Agent\IntegrationTests\UnboundedServices\unbounded-services-control.ps1"

switch ($testSuite) {
    "integration" { $testSuiteDll = "$rootDirectory\tests\Agent\IntegrationTests\IntegrationTests\bin\Release\net461\NewRelic.Agent.IntegrationTests.dll" }
    "unbounded" { $testSuiteDll = "$rootDirectory\tests\Agent\IntegrationTests\UnboundedIntegrationTests\bin\Release\net461\NewRelic.Agent.UnboundedIntegrationTests.dll" }
}

if (!(Test-Path $testSuiteDll)) {
    Write-Error "Test suite has not been built: $testSuiteDll" 
    exit
}

if ($secretsFilePath -ne "") {
    Get-Content $secretsFilePath | dotnet user-secrets set --project "$rootDirectory\tests\Agent\IntegrationTests\Shared"
}

if ($testSuite -eq "unbounded" -and $startUnboundedServices) {
    Invoke-Expression "$unboundedServicesControlPath -Start -StartDelaySeconds $unboundedServicesStartDelaySeconds -Platform $platform"
}

$expression = "$xUnitPath" + " " + "$testSuiteDll" + " " +  $xunitParams
Invoke-Expression $expression
$testResult = $LASTEXITCODE

if ($testSuite -eq "unbounded" -and $startUnboundedServices) {
    Invoke-Expression "$unboundedServicesControlPath -Stop -Platform $platform"
}

exit $testResult

