############################################################
# Copyright 2020 New Relic Corporation. All rights reserved.
# SPDX-License-Identifier: Apache-2.0
############################################################

$pathToDotnetAgentRepo = "C:\workspace\dotnet_agent"

[Environment]::SetEnvironmentVariable("COR_ENABLE_PROFILING", "1", "Machine")
[Environment]::SetEnvironmentVariable("COR_PROFILER", "{71DA0A04-7777-4EC6-9643-7D28B46A8A41}", "Machine")
[Environment]::SetEnvironmentVariable("COR_PROFILER_PATH", "$pathToDotnetAgentRepo\Agent\New Relic Home x64\NewRelic.Profiler.dll", "Machine")
[Environment]::SetEnvironmentVariable("NEW_RELIC_HOME", "$pathToDotnetAgentRepo\Agent\New Relic Home x64", "Machine")
[Environment]::SetEnvironmentVariable("NEW_RELIC_HOST", "staging-collector.newrelic.com", "Machine")
[Environment]::SetEnvironmentVariable("NEW_RELIC_LICENSE_KEY", "b25fd3ca20fe323a9a7c4a092e48d62dc64cc61d", "Machine")
# .NET Core Stuff
[Environment]::SetEnvironmentVariable("CORECLR_ENABLE_PROFILING", "1", "Machine")
[Environment]::SetEnvironmentVariable("CORECLR_PROFILER", "{71DA0A04-7777-4EC6-9643-7D28B46A8A41}", "Machine")
[Environment]::SetEnvironmentVariable("CORECLR_PROFILER_PATH", "$pathToDotnetAgentRepo\Agent\New Relic Home x64 CoreClr\NewRelic.Profiler.dll", "Machine")
[Environment]::SetEnvironmentVariable("CORECLR_NEW_RELIC_HOME", "$pathToDotnetAgentRepo\Agent\New Relic Home x64 CoreClr", "Machine")
