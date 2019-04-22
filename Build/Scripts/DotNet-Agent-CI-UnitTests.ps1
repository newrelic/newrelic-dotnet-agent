Write-Host "Creating TestResults directory to temporarily get around nunit limitation"
mkdir TestResults

$testDllPatterns = @('*Tests.dll', '*Test.dll', '*Test.Legacy.dll')

Write-Host "Finding files for Framework NUnit tests"
$frameworkTestPaths = @('Tests\Agent\UnitTests', 'Tests\NewRelic.Core.Tests')
$frameworkTestFileNames = (Get-ChildItem -Recurse -Path $frameworkTestPaths -Include $testDllPatterns | Where-Object { !$_.FullName.Contains('obj\Release') } | Select Name -Unique)
$frameworkFiles = (Get-ChildItem -Recurse -Path $frameworkTestPaths -Include $testDllPatterns | Where-Object { !$_.FullName.Contains('obj\Release')  })

Write-Host "Building file list for Framework NUnit tests"
$frameworkUnitTestPaths = @()
for ($i = 0; $i -lt $frameworkTestFileNames.Length; $i++)
{
    $frameworkFiles | ForEach-Object { if ($_.Name -eq $frameworkTestFileNames[$i].Name) { $frameworkUnitTestPaths += $_.FullName; Continue } }
}

Write-Host "Executing Framework NUnit Tests:"
$frameworkUnitTestPaths | ForEach-Object { $_ }
& '.\Build\Tools\NUnit-Console\nunit3-console.exe ' $frameworkUnitTestPaths '--result=TestResults\NUnit2-results.xml;format=nunit2'

if ($LastExitCode -ne 0)
{
	exit $LastExitCode
}


Write-Host "Finding files for .NET Core NUnit tests"
$netCoreTestFileNames = (Get-ChildItem -Recurse -Path 'Tests\AwsLambda\UnitTests' -Include $testDllPatterns | Where-Object { !$_.FullName.Contains('obj\Release') } | Select Name -Unique)
$netCoreFiles = (Get-ChildItem -Recurse -Path 'Tests\AwsLambda\UnitTests' -Include $testDllPatterns | Where-Object { !$_.FullName.Contains('obj\Release')  })

Write-Host "Building file list for .NET Core NUnit tests"
$netCoreUnitTestPaths = @()

for ($i = 0; $i -lt $netCoreTestFileNames.Length; $i++)
{
    $netCoreFiles | ForEach-Object { if ($_.Name -eq $netCoreTestFileNames[$i].Name) { $netCoreUnitTestPaths += $_.FullName; Continue } }
}

Write-Host "Executing .NET Core NUnit Tests:"
$netCoreUnitTestPaths | ForEach-Object { $_ }

Write-Host "dotnet vstest " $netCoreUnitTestPaths " /Settings:.\Tests\AwsLambda\UnitTests\settings.runsettings"
dotnet vstest $netCoreUnitTestPaths /Settings:.\Tests\AwsLambda\UnitTests\settings.runsettings

if ($LastExitCode -ne 0)
{
	exit $LastExitCode
}