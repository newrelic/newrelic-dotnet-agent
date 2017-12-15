#Remove Agent Manually
Write-Host ""
Write-Host "---------------------------"
Write-Host "Starting Agent Cleanup Script"
Write-Host "---------------------------"
Write-Host ""

iisreset /stop

# Elevate
Write-Host "Elevating to Administrator"
$myWindowsID=[System.Security.Principal.WindowsIdentity]::GetCurrent()
$myWindowsPrincipal=new-object System.Security.Principal.WindowsPrincipal($myWindowsID)
$adminRole=[System.Security.Principal.WindowsBuiltInRole]::Administrator
if ($myWindowsPrincipal.IsInRole($adminRole))
{
    $Host.UI.RawUI.WindowTitle = $myInvocation.MyCommand.Definition + "(Elevated)"
    $Host.UI.RawUI.BackgroundColor = "DarkRed"
    clear-host
}
else
{
    $newProcess = new-object System.Diagnostics.ProcessStartInfo "PowerShell"
    $newProcess.Arguments = $myInvocation.MyCommand.Definition
    $newProcess.Verb = "runas"
    [System.Diagnostics.Process]::Start($newProcess)
    exit
}
 
# Run your code that needs to be elevated here
Write-Host "Elevated"
Write-Host -NoNewline "Removing Files... "
$paths = @(
    "C:\Program Files\New Relic\.Net Agent\",
    "C:\Program Files (x86)\New Relic\.Net Agent\",
    "C:\ProgramData\New Relic\.Net Agent\",
    "HKLM:\SOFTWARE\New Relic\.NET Agent"
)

foreach ($path in $paths)
{
    If (Test-Path "$path"){
        Remove-Item -Force -Recurse -Path "$path"
    }
}

Write-Host "Done"

Write-Host -NoNewline "Removing Profiler... "
Remove-ItemProperty -LiteralPath HKLM:SYSTEM\CurrentControlSet\Services\W3SVC -Name Environment
Remove-ItemProperty -LiteralPath HKLM:SYSTEM\CurrentControlSet\Services\WAS -Name Environment
Write-Host "Done"

Write-Host -NoNewline "Removing Install/Uninstall Keys... "
$keyBases = @(
    "HKLM:SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
    "HKLM:SOFTWARE\Classes\Installer\Products"
)

foreach ($keyBase in $keyBases)
{
    $keyPaths=@()
    Get-ChildItem -Path $keyBase | ForEach-Object -Process {
        $leaf = $_ | Split-Path -Leaf
        $data = Get-ItemProperty -LiteralPath "$keyBase\$leaf"
        if($data.DisplayName -like "New Relic .NET Agent*" -Or $data.ProductName -like "New Relic .NET Agent*")
        {
            $keyPaths += "$keyBase\$leaf"
        }
    }

    if($keyPaths.Count -gt 0)
    {
        Write-Host "Found Agent install/uninstall keys and removing them."
        foreach($keyPath in $keyPaths)
        {
            Remove-Item -Force -Recurse -LiteralPath "$keyPath"
        }
    }
}

Write-Host "Done"

Write-Host -NoNewline "Removing System Environment Variables... "
[Environment]::SetEnvironmentVariable("COR_PROFILER_PATH","", "Machine")
[Environment]::SetEnvironmentVariable("COR_ENABLE_PROFILING","", "Machine")
[Environment]::SetEnvironmentVariable("COR_PROFILER","", "Machine")
[Environment]::SetEnvironmentVariable("NEWRELIC_INSTALL_PATH","", "Machine")
Write-Host "Done"

Write-Host "Removal Completed"
Start-Sleep -s 10
Write-Host ""