$ErrorActionPreference = 'Stop'
$version = Get-Content -Path .\Version.txt

$architectures = @("x64", "x86")

foreach ($architecture in $architectures)
{
    $path = "$env:WORKSPACE\CopiedArtifacts\NewRelicServerMonitor_$architecture.msi"
    $newName = "NewRelicServerMonitor_$($architecture)_$($version).msi"
    Rename-Item -Path $path -NewName $newName
}