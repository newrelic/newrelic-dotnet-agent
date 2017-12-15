IF "%Override%"=="true" (
    C:\Users\Administrator\.nuget\packages\xunit.runner.console\2.1.0\tools\xunit.console.exe %WORKSPACE%\IntegrationTests\IntegrationTests\bin\Release\net451\NewRelic.Agent.IntegrationTests.dll %TestParams% -xml testresults.xml
) ELSE (
   C:\Users\Administrator\.nuget\packages\xunit.runner.console\2.1.0\tools\xunit.console.exe %WORKSPACE%\IntegrationTests\IntegrationTests\bin\Release\net451\NewRelic.Agent.IntegrationTests.dll -xml testresults.xml
)