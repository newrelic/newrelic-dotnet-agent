############################################################
# Copyright 2020 New Relic Corporation. All rights reserved.
# SPDX-License-Identifier: Apache-2.0
############################################################

Param (
    [string]$xunitParams = "",
    [string]$secretsFilePath = ""
)

$rootDirectory = Resolve-Path "$(Split-Path -Parent $PSCommandPath)\..\.."
$xUnitPath = Resolve-Path "$rootDirectory\build\Tools\XUnit-Console\xunit.console.exe"

$testDll = "$rootDirectory\tests\Agent\PlatformTests\PlatformTests\bin\Release\net461\PlatformTests.dll"

if (!(Test-Path $testDll)) {
    Write-Error "Test suite has not been built: $testDll" 
    exit
}

cat $secretsFilePath | dotnet user-secrets set --project "$rootDirectory\tests\Agent\IntegrationTests\Shared"

$expression = "$xUnitPath" + " " + "$testDll" + " " + $xunitParams + " " + "-parallel none" + " " + "-verbose -xml testresults.xml" 
Invoke-Expression $expression
