Param (
    [Parameter(Mandatory=$true)]
    [ValidateSet("integration", "unbounded")]
    [string]$testSuite,
    [string]$xunitParams = "",
    [switch]$saveWorkingFolders = $false,
    [string]$secretsFilePath = ""
)

if ($saveWorkingFolders) {
    $env:NR_DOTNET_TEST_SAVE_WORKING_DIRECTORY = 1;
}

$rootDirectory = Resolve-Path "$(Split-Path -Parent $PSCommandPath)\..\.."
$xUnitPath = Resolve-Path "$rootDirectory\build\Tools\XUnit-Console\xunit.console.exe"

switch ($testSuite) {
    "integration" { $testSuiteDll = "$rootDirectory\tests\Agent\IntegrationTests\IntegrationTests\bin\Release\net461\NewRelic.Agent.IntegrationTests.dll" }
    "unbounded" { $testSuiteDll = "$rootDirectory\tests\Agent\IntegrationTests\UnboundedIntegrationTests\bin\Release\net461\NewRelic.Agent.UnboundedIntegrationTests.dll" }
}

if (!(Test-Path $testSuiteDll)) {
    Write-Error "Test suite has not been built: $testSuiteDll" 
    exit
}

cat $secretsFilePath | dotnet user-secrets set --project "$rootDirectory\tests\Agent\IntegrationTests\Shared"

$expression = "$xUnitPath" + " " + "$testSuiteDll" + " " +  $xunitParams
Invoke-Expression $expression
