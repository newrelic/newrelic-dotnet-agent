############################################################
# Copyright 2022 New Relic Corporation. All rights reserved.
# SPDX-License-Identifier: Apache-2.0
############################################################

Param(
    [string]$BaseClassName = ""
)

if ($BaseClassName -eq "")
{
    Write-Host "Must specify a base class name.  Example:  -BaseClassName RabbitMqTestsBase"
    exit
}

$TestName = $BaseClassName.Substring(0, $BaseClassName.IndexOf("Base"))

$NetFrameworkVersions = "FWLatest","FW471","FW462"

$NetCoreVersions = "CoreLatest","Core50","Core31" 

foreach ($NetFrameworkVersion in $NetFrameworkVersions)
{
    $SubClassName = $TestName + $NetFrameworkVersion
    Write-Host "[NetFrameworkTest]"
    Write-Host ("public class {0}: {1}<ConsoleDynamicMethodFixture{2}>" -f $SubClassName, $BaseClassName, $NetFrameworkVersion)
    Write-Host "{"
    Write-Host ("    public {0} (ConsoleDynamicMethodFixture{1} fixture, ITestOutputHelper output)" -f $SubClassName, $NetFrameworkVersion)
    Write-Host "        : base(fixture, output)"
    Write-Host "    {"
    Write-Host "    }"
    Write-Host "}"
    Write-Host ""
}

foreach ($NetCoreVersion in $NetCoreVersions)
{
    $SubClassName = $TestName + $NetCoreVersion
    Write-Host "[NetCoreTest]"
    Write-Host ("public class {0}: {1}<ConsoleDynamicMethodFixture{2}>" -f $SubClassName, $BaseClassName, $NetCoreVersion)
    Write-Host "{"
    Write-Host ("    public {0} (ConsoleDynamicMethodFixture{1} fixture, ITestOutputHelper output)" -f $SubClassName, $NetCoreVersion)
    Write-Host "        : base(fixture, output)"
    Write-Host "    {"
    Write-Host "    }"
    Write-Host "}"
    Write-Host ""
}
