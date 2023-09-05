
# set the required environment variables for the .NET Agent
$env:CORECLR_ENABLE_PROFILING=1
$env:CORECLR_PROFILER="{36032161-FFC0-4B61-B559-F6C5D41BAE5A}"
$env:CORECLR_PROFILER_PATH="$PWD\bin\release\net7.0\newrelic\NewRelic.Profiler.dll"
$env:CORECLR_NEWRELIC_HOME="$PWD\bin\release\net7.0\newrelic"
$env:NEW_RELIC_LICENSE_KEY="<your-new-relic-license-key>" # *** Replace with your license key! *** 
$env:NEW_RELIC_APP_NAME="ConsoleSample"

# run the sample
dotnet run -c Release