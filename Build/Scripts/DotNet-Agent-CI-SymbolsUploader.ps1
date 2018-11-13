$ErrorActionPreference = "Stop"

$msi= Get-ChildItem -Path "$ENV:WORKSPACE\Agent\_build\x64-Release\Installer\*.msi"
$version = $msi.Name.TrimStart('NewRelicAgent_x64_').TrimEnd('.msi')
cd "C:\Program Files (x86)\Windows Kits\10\Debuggers\x64"
Write-Host "Uploading $version"
.\symstore add /r /s "\\nrsymbols.file.core.windows.net\symbols\symbols" /f "$ENV:WORKSPACE\*.pdb" /t "New Relic .NET Agent" /v "$version" /o
.\symstore add /r /s "\\nrsymbols.file.core.windows.net\symbols\symbols" /f "$ENV:WORKSPACE\*.dll" /t "New Relic .NET Agent" /v "$version" /o