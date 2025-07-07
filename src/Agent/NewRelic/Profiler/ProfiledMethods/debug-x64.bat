set COR_ENABLE_PROFILING=1
set COR_PROFILER={71DA0A04-7777-4EC6-9643-7D28B46A8A41}
set COR_PROFILER_PATH=TestNewRelicHome\NewRelic.Profiler.dll
set NEW_RELIC_HOME=TestNewRelicHome\
set NEW_RELIC_INSTALL_PATH=TestNewRelicHome\
set NEW_RELIC_PROFILER_LOG_DIRECTORY=TestNewRelicHome\Logs\
rem The commented commandline will be used when the socket related bug in nunit console v3 is fixed (https://github.com/nunit/nunit-console/issues/171) 
rem start "NUnit x64" "..\..\..\..\packages\NUnit.ConsoleRunner.3.6.1\tools\nunit3-console.exe" "ProfiledMethods.dll" "--result=TestResult.xml;format=nunit3"
start "NUnit x64" "..\..\..\..\packages\NUnit.Runners.2.6.3\tools\nunit-console.exe" "ProfiledMethods.dll"
