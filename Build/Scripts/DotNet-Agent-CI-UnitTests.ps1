Write-Host "Finding files for NUnit tests"
$fileNames = (Get-ChildItem -Recurse -Path 'Tests\UnitTests\' -Include @('*Tests.dll', '*Test.dll', '*Test.Legacy.dll') | Where { !$_.FullName.Contains('obj\Release')} | Select Name -Unique)
$files = (Get-ChildItem -Recurse -Path 'Tests\UnitTests\' -Include @('*Tests.dll', '*Test.dll', '*Test.Legacy.dll') | Where { !$_.FullName.Contains('obj\Release')})

Write-Host "Building file list for NUnit tests"
$unitTestPaths = @()
for ($i = 0; $i -lt $fileNames.Length; $i++)
{
    $files | % { if ($_.Name -eq $fileNames[$i].Name) { $unitTestPaths += $_.FullName; Continue } }
}

Write-Host "Executing NUnit Tests:"
$unitTestPaths | % { $_ }
# & 'C:\Program Files (x86)\NUnit 2.6.4\bin\nunit-console-x86' $unitTestPaths '-xml=TestResults\nunit-results.xml'
& 'C:\Program Files (x86)\NUnit.org\nunit-console\nunit3-console.exe' $unitTestPaths '--result=TestResults\NUnit2-results.xml;format=nunit2'
if ($LastExitCode -ne 0)
{
	exit $LastExitCode
}
