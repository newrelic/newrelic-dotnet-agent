:: Copyright 2020 New Relic Corporation. All rights reserved.
:: SPDX-License-Identifier: Apache-2.0

IF "%Override%"=="true" (
    C:\Users\Administrator\.nuget\packages\xunit.runner.console\2.1.0\tools\xunit.console.exe %WORKSPACE%\IntegrationTests\UnboundedIntegrationTests\bin\Release\net452\NewRelic.Agent.UnboundedIntegrationTests.dll %TestParams% -xml testresults.xml
) ELSE (
   C:\Users\Administrator\.nuget\packages\xunit.runner.console\2.1.0\tools\xunit.console.exe %WORKSPACE%\IntegrationTests\UnboundedIntegrationTests\bin\Release\net452\NewRelic.Agent.UnboundedIntegrationTests.dll -xml testresults.xml
)