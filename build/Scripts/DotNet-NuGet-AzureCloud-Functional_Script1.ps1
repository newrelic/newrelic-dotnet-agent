# Copyright 2020 New Relic Corporation. All rights reserved.
# SPDX-License-Identifier: Apache-2.0

$ErrorActionPreference = "SilentlyContinue"
. .\agent-build-scripts\windows\common\powershell\nuGet.ps1
RestoreNuGetPackages NewRelicAzureCloudCI\NewRelicAzureCloudCI.sln "https://www.nuget.org/api/v2"