# This script needs to be run on a machine that has been set up to access our
# Azure Storage Account at \\nrsymbols.file.core.windows.net\symbols\symbols

# The following set up procedures only need to be run once on a machine.
# The Azure PowerShell module is required. If it hasn't been installed yet, run: Install-Module -Name Az -AllowClobber

# From powershell run Connect-AzAccount to log in to our Azure subscription interactively.
# Then run the following commands:

# $resourceGroupName = "DotNetBuildSystems"
# $storageAccountName = "nrsymbols"
# $storageAccount = Get-AzStorageAccount -ResourceGroupName $resourceGroupName -Name $storageAccountName
# $storageAccountKeys = Get-AzStorageAccountKey -ResourceGroupName $resourceGroupName -Name $storageAccountName
# Invoke-Expression -Command ("cmdkey /add:$([System.Uri]::new($storageAccount.Context.FileEndPoint).Host) /user:AZURE\$($storageAccount.StorageAccountName) /pass:$($storageAccountKeys[0].Value)")

# If successful, you should be able to open up Windows Explorer and access \\nrsymbols.file.core.windows.net\symbols\symbols

$ErrorActionPreference = "Stop"

$msi= Get-ChildItem -Path "$ENV:WORKSPACE\Agent\_build\x64-Release\Installer\*.msi"
$version = $msi.Name.TrimStart('NewRelicAgent_x64_').TrimEnd('.msi')
cd "C:\Program Files (x86)\Windows Kits\10\Debuggers\x64"
Write-Host "Uploading $version"
.\symstore add /r /s "\\nrsymbols.file.core.windows.net\symbols\symbols" /f "$ENV:WORKSPACE\*.pdb" /t "New Relic .NET Agent" /v "$version" /o
.\symstore add /r /s "\\nrsymbols.file.core.windows.net\symbols\symbols" /f "$ENV:WORKSPACE\*.dll" /t "New Relic .NET Agent" /v "$version" /o