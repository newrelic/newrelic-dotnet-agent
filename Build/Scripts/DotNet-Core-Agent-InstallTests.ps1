# Core Agent Install Tests
#
# This is a Powershell script run in Jenkins
#	Job Name: DotNet-Core-Agent-InstallTests 
#	Job Location: https://dotnet-build.pdx.vm.datanerd.us/job/DotNet-Core-Agent-InstallTests/

$installfilesPath = Join-Path (pwd) "install_files"
$destination = Join-Path (pwd) "install_location"
$logDir = Join-Path (pwd) "log_dir"
$installType = "global"
$licenseKey = "12345"

Function Elevate-To-Admin {
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
}

Function Unpack-Installer-Zip {
    $installerZip = Get-Item .\newrelic-netcore20-agent-win-installer_*.zip
    $installerZip | Should BeLike "*newrelic-netcore20*"
    $installFilesDir = New-Item $installFilesPath -ItemType Directory
    [System.Reflection.Assembly]::LoadWithPartialName('System.IO.Compression.FileSystem') | Out-Null
    [System.IO.Compression.ZipFile]::ExtractToDirectory($installerZip, $installFilesDir) | Out-Null
    $? | Should BeTrue
    return $true
}

Function Install-Agent {
    .\install_files\installAgent.ps1 -destination $destination -installType $installType -licensekey $licenseKey -logDir $logDir | Out-Null
    $? | Should BeTrue
    return $true
}

Function Check-Environment-Variables {
    $profilerPath = [Environment]::GetEnvironmentVariable("CORECLR_PROFILER_PATH","Machine")
    $enableProfiling = [Environment]::GetEnvironmentVariable("CORECLR_ENABLE_PROFILING","Machine")
    $profilerGuid = [Environment]::GetEnvironmentVariable("CORECLR_PROFILER","Machine")
    $nrHome = [Environment]::GetEnvironmentVariable("CORECLR_NEWRELIC_HOME","Machine")

    $profilerPath | Should Be "$destination\NewRelic.Profiler.dll"
    $enableProfiling | Should Be 1
    $profilerGuid | Should Be "{36032161-FFC0-4B61-B559-F6C5D41BAE5A}"
    $nrHome | Should Be "$destination"

    return $true
}

Function Clean-Install-Dir {
    Remove-Item $destination -Recurse
    $destination | Should Not Exist
    Remove-Item $installFilesPath -Recurse
    $installFilesPath | Should Not Exist
    Remove-Item $logDir -Recurse
    $logDir | Should Not Exist
    return $true    
}

Function Clean-Environment-Variables {
    # Clean Environment Variables
    [Environment]::SetEnvironmentVariable("CORECLR_PROFILER_PATH","", "Machine")
    [Environment]::SetEnvironmentVariable("CORECLR_ENABLE_PROFILING","", "Machine")
    [Environment]::SetEnvironmentVariable("CORECLR_PROFILER","", "Machine")
    [Environment]::SetEnvironmentVariable("CORECLR_NEWRELIC_HOME", "", "Machine")
    return $true
}


Elevate-To-Admin
Describe 'Install-Core-Agent' {
    Context 'Unpack the zip and run the installer' {
        It "Unpack the installer zip" {
            Unpack-Installer-Zip | Should Be $true
        }
        It "Run the installer script" {
            Install-Agent | Should Be $true
        }
    }
    Context 'Verify the install' {
        It "Verify the log dir exists" {
            $logDir | Should Exist
        }
        It "Verify the environment variables" {
            Check-Environment-Variables | Should Be $true
        }
    }
    Context 'Clean up after the test' {
        It "Clean the install dir" {
            Clean-Install-Dir | Should Be $true
        }
        It "Clean the evironment variables" {
            Clean-Environment-Variables | Should Be $true
        }
    }
}
