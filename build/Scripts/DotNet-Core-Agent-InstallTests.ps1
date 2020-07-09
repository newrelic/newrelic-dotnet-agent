#$ErrorActionPreference = 'SilentlyContinue'
$errorCode = 0
$installfilesPath=".\install_files"
$expectedInstalledFileCount = 35

Write-Host ""
Write-Host "Elevating to Administrator"
Write-Host "===================="
$myWindowsID=[System.Security.Principal.WindowsIdentity]::GetCurrent()
$myWindowsPrincipal=new-object System.Security.Principal.WindowsPrincipal($myWindowsID)
$adminRole=[System.Security.Principal.WindowsBuiltInRole]::Administrator
if ($myWindowsPrincipal.IsInRole($adminRole))
{
    $Host.UI.RawUI.WindowTitle = $myInvocation.MyCommand.Definition + "(Elevated)"
    clear-host
}
else
{
    $newProcess = new-object System.Diagnostics.ProcessStartInfo "PowerShell"
    $newProcess.Arguments = $myInvocation.MyCommand.Definition
    $newProcess.Verb = "runas"
    [System.Diagnostics.Process]::Start($newProcess) | Out-Null
    exit
}

Write-Host ""
Write-Host "Decompressing core installer zip file."
Write-Host "===================="
$installerZip = Get-Item .\newrelic.netcore20-agent-installer_*.zip
$installFilesDir = New-Item $installFilesPath -ItemType Directory
[System.Reflection.Assembly]::LoadWithPartialName('System.IO.Compression.FileSystem') | Out-Null
[System.IO.Compression.ZipFile]::ExtractToDirectory($installerZip, $installFilesDir) | Out-Null

# Install Agent
Write-Host ""
Write-Host "Installing Agent"
Write-Host "===================="
$destination = ".\install_location"
$installType = "global"
$licenseKey = "12345"
$logDir = ".\log_dir"

.\install_files\install_agent.ps1 -destination $destination -installType $installType -licensekey $licenseKey -logDir $logDir | Out-Null

Write-Host "--> SUCCESS: Agent installed"

# Check install location
Write-Host ""
Write-Host "Validating install"
Write-Host "===================="
$fileCount = (Get-ChildItem $destination -Recurse).Length
if ($fileCount -eq $expectedInstalledFileCount) {
    Write-Host "--> SUCCESS: Install files count validated ($fileCount) of $expectedInstalledFileCount"
}
else {
    $errorCode = 1
    Write-Host "--> ERROR: Install files NOT validated  ($fileCount) of $expectedInstalledFileCount"
}

# Check for log directory
Write-Host ""
Write-Host "Checking for custom log directory"
Write-Host "===================="
$logDirExists = ((Get-Item $logDir).Length -eq 1)
if ($logDirExists) {
    Write-Host "--> SUCCESS: Log Directory Exits"
}
else {
    $errorCode = 1
    Write-Host "--> ERROR: Log Directory DOES NOT exist"
}

# Check environment variables
Write-Host ""
Write-Host "Checking for custom environment variables"
Write-Host "===================="
$profilerPath = [Environment]::GetEnvironmentVariable("CORECLR_PROFILER_PATH","Machine")
$enableProfiling = [Environment]::GetEnvironmentVariable("CORECLR_ENABLE_PROFILING","Machine")
$profilerGuid = [Environment]::GetEnvironmentVariable("CORECLR_PROFILER","Machine")
$nrHome = [Environment]::GetEnvironmentVariable("CORECLR_NEWRELIC_HOME","Machine")

if ( $profilerPath -And $enableProfiling -And $profilerGuid -And $nrHome) {
    Write-Host "--> SUCCESS: Environment variables present"
} else {
    $errorCode = 1
  Write-Host "--> ERROR: Environment variables NOT present"
}

# Clean install directory
#Write-Host ""
#Write-Host "Cleaning up files"
#Write-Host "===================="
#Remove-Item $destination -Recurse
#Remove-Item $installFilesPath -Recurse
#Remove-Item $logDir -Recurse
#Write-Host "--> SUCCESS: Install files and location removed"

# Clean Environment Variables
Write-Host ""
Write-Host "Cleaning up Environment Variables"
Write-Host "===================="
[Environment]::SetEnvironmentVariable("CORECLR_PROFILER_PATH","", "Machine")
[Environment]::SetEnvironmentVariable("CORECLR_ENABLE_PROFILING","", "Machine")
[Environment]::SetEnvironmentVariable("CORECLR_PROFILER","", "Machine")
[Environment]::SetEnvironmentVariable("CORECLR_NEWRELIC_HOME", "", "Machine")
Write-Host "--> SUCCESS: Environment variables removed"

Write-Host ""
Write-Host "===================="
if ( $errorCode -eq 0) {
    Write-Host "TEST SUCCESS"
} else {
    Write-Host "TEST FAILURE"
}
Write-Host "===================="

exit $errorCode