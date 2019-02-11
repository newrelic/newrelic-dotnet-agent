Write-Host ""
Write-Host "********"
Write-Host "Building and Running Linux Tests"
Write-Host "********"

$baseProfilerPath = (Get-Item (Split-Path $script:MyInvocation.MyCommand.Path)).parent.parent.FullName
Push-Location $baseProfilerPath

$env:CORECLR_NEWRELIC_HOME = "$baseProfilerPath\linux\test\agent"
Write-Host "Set CORECLR_NEWRELIC_HOME = $env:CORECLR_NEWRELIC_HOME"

Write-Host "docker-compose build test"
docker-compose build test
if ($LastExitCode -ne 0) {
    exit $LastExitCode
}

Write-Host 'docker-compose run test bash -c  ...'
Write-Host '... "cd /test/custom_attributes && CORECLR_ENABLE_PROFILING=0 dotnet build ...'
Write-Host '... && CORECLR_NEWRELIC_INSTALL_PATH=/test/custom_attributes/bin/Debug/netcoreapp2.0/custom_attributes.dll ...'
Write-Host '... dotnet vstest bin/Debug/netcoreapp2.0/custom_attributes.dll"'
docker-compose run test bash -c "cd /test/custom_attributes && CORECLR_ENABLE_PROFILING=0 dotnet build && CORECLR_NEWRELIC_INSTALL_PATH=/test/custom_attributes/bin/Debug/netcoreapp2.0/custom_attributes.dll dotnet vstest bin/Debug/netcoreapp2.0/custom_attributes.dll"
if ($LastExitCode -ne 0) {
    exit $LastExitCode
}

Write-Host 'docker-compose run test_centos bash -c ...'
Write-Host '... "cd /test/custom_attributes && CORECLR_ENABLE_PROFILING=0 dotnet build && ...'
Write-Host '... CORECLR_NEWRELIC_INSTALL_PATH=/test/custom_attributes/bin/Debug/netcoreapp2.0/custom_attributes.dll ...'
Write-Host '... dotnet vstest bin/Debug/netcoreapp2.0/custom_attributes.dll"'
docker-compose run test_centos bash -c "cd /test/custom_attributes && CORECLR_ENABLE_PROFILING=0 dotnet build && CORECLR_NEWRELIC_INSTALL_PATH=/test/custom_attributes/bin/Debug/netcoreapp2.0/custom_attributes.dll dotnet vstest bin/Debug/netcoreapp2.0/custom_attributes.dll"
if ($LastExitCode -ne 0) {
    exit $LastExitCode
}

Write-Host ""
Write-Host "********"
Write-Host "Clean up old containers"
Write-Host "********"

Write-Host "Cleaning up old containers"
Write-Host 'Running command: docker container prune --force --filter "until=60m"'
docker container prune --force --filter "until=60m"

Pop-Location
