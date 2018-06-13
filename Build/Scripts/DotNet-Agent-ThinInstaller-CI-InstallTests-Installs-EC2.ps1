# Scripted Installer Tests
#
# This is a Powershell script run in Jenkins
#	Job Name: DotNet-Agent-ThinInstaller-CI-InstallTests-Installs-EC2-PROTO 
#	Job Location: https://dotnet-build.pdx.vm.datanerd.us/job/DotNet-Agent-ThinInstaller-CI-InstallTests-Installs-EC2-PROTO
#

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
    $newProcess = new-object System.Diagnostics.ProcessStartInfo "PowerShell";
    $newProcess.Arguments = $myInvocation.MyCommand.Definition;
    $newProcess.Verb = "runas";
    [System.Diagnostics.Process]::Start($newProcess);
    exit
}
 
# Run your code that needs to be elevated here
Write-Host "Elevated"
$ErrorActionPreference = "Stop"


$AgentVersion = [IO.File]::ReadAllText("Installer\AgentVersion.txt").Trim()

# Constants
Set-Variable -name DefaultInstallLocation -option Constant -value  "$env:ProgramFiles\New Relic\.NET Agent"

Function Expand-Zip($file, $destination)
{
    Write-Host "Expanding $file to $destination"
    $shellApplication = New-Object -com shell.application
    $zipPackage = $shellApplication.NameSpace($file)
    $destinationFolder = $shellApplication.NameSpace($destination)
    $destinationFolder.Copyhere($zipPackage.Items(),20)
}

Function CheckForCleanInstallSuccess([string] $frameworkVersion)
{
    Write-Host ""
    Write-Host "---------------------------------------------------"
    Write-Host " Install Success Check for Framework Version $frameworkVersion"
    Write-Host "---------------------------------------------------"
    Write-Host ""
	
    $CurrentLocation = [string](Get-Location)
	
    $Destination = "C:\AgentThinInstaller"
    $Source = "$env:WORKSPACE\NewRelic.Agent.Installer.$AgentVersion.zip"
	

    If(Test-path $Destination) {Remove-item $Destination -Recurse:$true}
    New-Item $Destination -type directory

    #Only works in 4.5
    #Add-Type -assembly "system.io.compression.filesystem"
    #[io.compression.zipfile]::ExtractToDirectory($Source, $Destination)

    #Works in any version, sleep needed since method is async
    Expand-Zip $Source $Destination
    Start-Sleep -seconds 10

    # Install the Agent.

    cd C:\AgentThinInstaller

    .\install.cmd -licenseKey 123 | Out-Host
	
	$exitCode = $LastExitCode
    
	If ($exitCode -ne 0){
		Write-Host "There was an error attempting to install the .NET Agent - Code: $exitCode"
	}
	else{
        Write-Host ""
        Write-Host "---------------------------"
	Write-Host "Installing - Passed"
        Write-Host "---------------------------"
        Write-Host ""

		If ( -Not (VerifyInstallState $DefaultInstallLocation )) {
			Write-Host "Failed to validate the install state."
			$exitCode = 1
		}
		else {
	        # Test uninstalling
                
                cd C:\AgentThinInstaller
                # Write-Host "WP 1: "  (Get-Item -Path ".\" -Verbose).FullName
               
	        .\uninstall.cmd -Force True
	        
	        $exitCode = $LastExitCode
	        
	        If ($exitCode -ne 0){
	            Write-Host "There was an error attempting to uninstall the .NET Agent - Code: $exitCode"
	        }
	        else{
				If (-Not (ValidateUninstall)) {
					Write-Host "Uninstall did not pass verification"
					$exitCode = 1
				}
				else {
		            Write-Host ""
		            Write-Host "---------------------------"
		            Write-Host "Uninstalling - Passed"
		            Write-Host "---------------------------"
		            Write-Host ""
				}
	        }
		}        
	}
	
    cd $CurrentLocation

}

Function CheckForInstallSameOrNewerVersionSuccess([string] $frameworkVersion)
{
    Write-Host ""
    Write-Host "---------------------------------"
    Write-Host " Install Same Version Or Upgrade Success Check. Framework Version $frameworkVersion"
    Write-Host "---------------------------------"
    Write-Host ""
	
	$CurrentLocation = [string](Get-Location)
	
    $Destination = "C:\AgentThinInstaller"
    $Source = "$env:WORKSPACE\NewRelic.Agent.Installer.$AgentVersion.zip"
	

    If(Test-path $Destination) {Remove-item $Destination -Recurse:$true}
    New-Item $Destination -type directory

    #Only works in 4.5
    #Add-Type -assembly "system.io.compression.filesystem"
    #[io.compression.zipfile]::ExtractToDirectory($Source, $Destination)

    #Works in any version, sleep needed since method is async
    Expand-Zip $Source $Destination
    Start-Sleep -seconds 10

    # Install the agent.

    cd C:\AgentThinInstaller

    .\install.cmd -licenseKey 123 | Out-Host
	
	$exitCode = $LastExitCode
    
	If ($exitCode -ne 0){
		Write-Host "There was an error attempting to install the .NET Agent - Code: $exitCode"
        exit 1
	}
    
    # Install the agent again.
    .\install.cmd -licenseKey 123 | Out-Host
    $exitCode = $LastExitCode
    
    If ($exitCode -ne 0){
		Write-Host "There was an error attempting to install same/newer the .NET Agent - Code: $exitCode"
	}
    else{
        Write-Host ""
        Write-Host "---------------------------"
		Write-Host "Installing same or newer version of the agent - Passed"
        Write-Host "---------------------------"
        Write-Host ""
        
		If ( -Not (VerifyInstallState $DefaultInstallLocation)) {
			Write-Host "Failed to validate the install state."
			$exitCode = 1
		}
		else {
	        # Test uninstalling
                cd C:\AgentThinInstaller
                # Write-Host "WP 2: "  (Get-Item -Path ".\" -Verbose).FullName
	        .\uninstall.cmd -Force True
	        
	        $exitCode = $LastExitCode
	        
	        If ($exitCode -ne 0){
	            Write-Host "There was an error attempting to uninstall the .NET Agent - Code: $exitCode"
	        }
	        else{
				If (-Not (ValidateUninstall)) {
					Write-Host "Uninstall did not pass verification"
					$exitCode = 1
				}
				else {
		            Write-Host ""
		            Write-Host "---------------------------"
		            Write-Host "Uninstalling - Passed"
		            Write-Host "---------------------------"
		            Write-Host ""
				}
	        }
		}        
	}
	
    cd $CurrentLocation
}


Function CheckForInstallSameOrNewerVersionWithNewKeySuccess([string] $frameworkVersion)
{
    Write-Host ""
    Write-Host "--------------------------------"
    Write-Host " Install Same Version Or Upgrade Success Check. Framework Version $frameworkVersion"
    Write-Host "--------------------------------"
    Write-Host ""
	
	$CurrentLocation = [string](Get-Location)
	
    $Destination = "C:\AgentThinInstaller"
    $Source = "$env:WORKSPACE\NewRelic.Agent.Installer.$AgentVersion.zip"
	

    If(Test-path $Destination) {Remove-item $Destination -Recurse:$true}
    New-Item $Destination -type directory

    #Only works in 4.5
    #Add-Type -assembly "system.io.compression.filesystem"
    #[io.compression.zipfile]::ExtractToDirectory($Source, $Destination)

    #Works in any version, sleep needed since method is async
    Expand-Zip $Source $Destination
    Start-Sleep -seconds 10

    # Install the agent.

    cd C:\AgentThinInstaller

    .\install.cmd -licenseKey 123 | Out-Host
	
	$exitCode = $LastExitCode
    
	If ($exitCode -ne 0){
		Write-Host "There was an error attempting to install the .NET Agent - Code: $exitCode"
        exit 1
	}
    
    # Install the agent again.
    .\install.cmd -licenseKey abc -forceLicenseKey | Out-Host
    $exitCode = $LastExitCode
    
    If ($exitCode -ne 0){
		Write-Host "There was an error overinstalling the agent with a new license key and -forceLicenceKey present - Code: $exitCode"
	}
    else{
        Write-Host ""
        Write-Host "---------------------------"
		Write-Host "Installing same or newer version of the agent with a new license key and -forceLicenceKey - Passed"
        Write-Host "---------------------------"
        Write-Host ""
        
		If ( -Not (VerifyInstallState $DefaultInstallLocation)) {
			Write-Host "Failed to validate the install state."
			$exitCode = 1
		}
		else {
	        # Test uninstalling
	        cd C:\AgentThinInstaller
                #Write-Host "WP 3: "  (Get-Item -Path ".\" -Verbose).FullName
                .\uninstall.cmd -Force True
	        
	        $exitCode = $LastExitCode
	        
	        If ($exitCode -ne 0){
	            Write-Host "There was an error attempting to uninstall the .NET Agent - Code: $exitCode"
	        }
	        else{
				If (-Not (ValidateUninstall)) {
					Write-Host "Uninstall did not pass verification"
					$exitCode = 1
				}
				else {
		            Write-Host ""
		            Write-Host "---------------------------"
		            Write-Host "Uninstalling - Passed"
		            Write-Host "---------------------------"
		            Write-Host ""
				}
	        }
		}        
	}
	
    cd $CurrentLocation

}



Function CheckForCleanInstallFailure([string] $frameworkVersion)
{
    Write-Host ""
    Write-Host "-------------------------------------------"
    Write-Host " Install Failure Check. Framework Version $frameworkVersion"
    Write-Host "-------------------------------------------"
    Write-Host ""
	
	$CurrentLocation = [string](Get-Location)

    $Destination = "C:\AgentThinInstaller"
    $Source = "$env:WORKSPACE\NewRelic.Agent.Installer.$AgentVersion.zip"
	

    If(Test-path $Destination) {Remove-item $Destination -Recurse:$true}
    New-Item $Destination -type directory

    #Only works in 4.5
    #Add-Type -assembly "system.io.compression.filesystem"
    #[io.compression.zipfile]::ExtractToDirectory($Source, $Destination)

    #Works in any version, sleep needed since method is async
    Expand-Zip $Source $Destination
    Start-Sleep -seconds 10

    # Install the Agent.

    cd C:\AgentThinInstaller

    .\install.cmd -licenseKey 123 | Out-Host
	
	$exitCode = $LastExitCode
	
        # Setting last exit code which is an internal Windows exit code. The updated Jenkins automatically 
        # exits if it is set to non-zero. Seeing if this fixes that.
        $global:LASTEXITCODE = $null

	cd $CurrentLocation
	
	If ($exitCode -eq 0){
		Write-Host "The installer should fail. FrameworkVersion $frameworkVersion"
		exit 1
	}
	else{
		Write-Host "FrameworkVersion $frameworkVersion - Passed"
                $exitCode = 0
                $LastExitCode = 0
	}
	
	Write-Host "FrameworkVersion $frameworkVersion Exit Code: $exitCode"

}


Function CheckForCustomDirectoryInstallSuccess([string] $frameworkVersion)
{
    Write-Host ""
    Write-Host "--------------------------------------------------"
    Write-Host " Custom Directory Install Success Check. Framework Version $frameworkVersion"
    Write-Host "--------------------------------------------------"
    Write-Host ""
	
	$CurrentLocation = [string](Get-Location)
	
    $Destination = "C:\AgentThinInstaller"
    $Source = "$env:WORKSPACE\NewRelic.Agent.Installer.$AgentVersion.zip"
	

    If(Test-path $Destination) {Remove-item $Destination -Recurse:$true}
    New-Item $Destination -type directory

    #Only works in 4.5
    #Add-Type -assembly "system.io.compression.filesystem"
    #[io.compression.zipfile]::ExtractToDirectory($Source, $Destination)

    #Works in any version, sleep needed since method is async
    Expand-Zip $Source $Destination
    Start-Sleep -seconds 10

    # Install the Agent.

    cd C:\AgentThinInstaller
    
    $CustomDirectory = "C:\CutomAgentInstallDirectory"

    .\install.cmd -licenseKey 123 -installPath $CustomDirectory | Out-Host
	
	$exitCode = $LastExitCode
    
	If ($exitCode -ne 0){
		Write-Host "There was an error attempting to install the .NET Agent to $CustomDirectory - Code: $exitCode"
	}
	else{
        
        #Check if Extensions, Tools and x86 directories exist.
        If((Test-Path "$CustomDirectory\Extensions") -and 
            (Test-Path "$CustomDirectory\Tools") -and
            (Test-Path "$CustomDirectory\x86")){
            
                Write-Host ""
                Write-Host "---------------------------"
                Write-Host "Found Agent Folders. Custom Directory Install - Passed"
                Write-Host "---------------------------"
                Write-Host ""
        
				If ( -Not (VerifyInstallState $CustomDirectory )) {
					Write-Host "Failed to validate the install state."
					$exitCode = 1
				}
				else {
		            # Test uninstalling
                            cd C:\AgentThinInstaller
		            .\uninstall.cmd -Force True
		            #Write-Host "WP 4: "  (Get-Item -Path ".\" -Verbose).FullName
		            $exitCode = $LastExitCode
		        
		            If ($exitCode -ne 0){
		                Write-Host "There was an error attempting to uninstall the .NET Agent - Code: $exitCode"
		            }
		            else {
						If (-Not (ValidateUninstall)) {
							Write-Host "Uninstall did not pass verification"
							$exitCode = 1
						}
						else {
			                Write-Host ""
			                Write-Host "---------------------------"
			                Write-Host "Uninstalling - Passed"
			                Write-Host "---------------------------"
			                Write-Host ""
						}
		            }
				}
        }
        else{
                Write-Host ""
                Write-Host "---------------------------"
                Write-Host "Could not find Extensions, Tools and x86 directories after installing - Failed"
                Write-Host "---------------------------"
                Write-Host ""
                
                $exitCode = 1
        }
	}
	
    cd $CurrentLocation
}

Function VerifyInstallState ([string] $installPath)
{
	If ((!(ValidateRequiredTargetFolderFilesExist $installPath)) -or
		(!(ValidateRequiredProgramDataFilesExist)) -or
		(!(ValidateRegistryKeys)) -or
		(!(ValidateRegistryItemProperties))) {
		return $false
	}
	else {
		return $true
	}
}

Function ValidateRequiredTargetFolderFilesExist([string] $targetFullPath)
{
	cd $targetFullPath
	
	If (
		(!(CheckFileExists("Extensions\NewRelic.Core.dll"))) -or
		(!(CheckFileExists("Extensions\NewRelic.Providers.Storage.HttpContext.dll"))) -or
		(!(CheckFileExists("Extensions\NewRelic.Providers.TransactionContext.Default.dll"))) -or
		(!(CheckFileExists("Extensions\NewRelic.Providers.Storage.OperationContext.dll"))) -or
		(!(CheckFileExists("Extensions\NewRelic.Providers.Wrapper.Asp35.dll"))) -or
		(!(CheckFileExists("Extensions\NewRelic.Providers.Wrapper.MongoDB.dll"))) -or
		(!(CheckFileExists("Extensions\NewRelic.Providers.Wrapper.MongoDB26.dll"))) -or
		(!(CheckFileExists("Extensions\NewRelic.Providers.Wrapper.Mvc3.dll"))) -or
		(!(CheckFileExists("Extensions\NewRelic.Providers.Wrapper.NServiceBus.dll"))) -or
		(!(CheckFileExists("Extensions\NewRelic.Providers.Wrapper.Wcf3.dll"))) -or
		(!(CheckFileExists("Extensions\NewRelic.Providers.Wrapper.WebApi1.dll"))) -or
		(!(CheckFileExists("Extensions\NewRelic.Providers.Wrapper.WebApi2.dll"))) -or
#		(!(CheckFileExists("Tools\flush_dotnet_temp.cmd"))) -or
		(!(CheckFileExists("Tools\Ionic.Zip.Reduced.dll"))) -or
		(!(CheckFileExists("Tools\Microsoft.Web.Administration.dll"))) -or
		(!(CheckFileExists("Tools\NewRelic.SupportCollator.Client.dll"))) -or
		(!(CheckFileExists("Tools\NewRelic.SupportCollator.Client.Wrapper.dll"))) -or
		(!(CheckFileExists("Tools\NewRelicStatusMonitor.exe"))) -or
		(!(CheckFileExists("Tools\NewRelicStatusMonitor.exe.config"))) -or
		(!(CheckFileExists("Tools\Newtonsoft.Json.dll"))) -or
		(!(CheckFileExists("Tools\Ninject.dll"))) -or
		(!(CheckFileExists("Tools\System.Management.Automation.dll"))) -or
		(!(CheckFileExists("Tools\SystemInterface.dll"))) -or
		(!(CheckFileExists("Tools\SystemWrapper.dll"))) -or
		(!(CheckFileExists("Tools\TrayManager.dll"))) -or
#		(!(CheckFileExists("default_newrelic.config"))) -or
#		(!(CheckFileExists("License.txt"))) -or
		(!(CheckFileExists("NewRelic.Agent.Core.dll"))) -or
		(!(CheckFileExists("NewRelic.Agent.Extensions.dll"))) -or
#		(!(CheckFileExists("NewRelic.Api.Agent.dll"))) -or
		(!(CheckFileExists("NewRelic.Profiler.dll")))) {
		return $false
	}
	else {
		return $true
	}
}

Function ValidateRequiredProgramDataFilesExist
{
	cd "$env:ProgramData\New Relic\.NET Agent"
	
	If ((!(CheckFileExists("Extensions\extension.xsd"))) -or
        (!(CheckFileExists("Extensions\NewRelic.Providers.Wrapper.Asp35.Instrumentation.xml"))) -or
        (!(CheckFileExists("Extensions\NewRelic.Providers.Wrapper.MongoDB.Instrumentation.xml"))) -or
        (!(CheckFileExists("Extensions\NewRelic.Providers.Wrapper.MongoDB26.Instrumentation.xml"))) -or
        (!(CheckFileExists("Extensions\NewRelic.Providers.Wrapper.Mvc3.Instrumentation.xml"))) -or
        (!(CheckFileExists("Extensions\NewRelic.Providers.Wrapper.NServiceBus.Instrumentation.xml"))) -or
        (!(CheckFileExists("Extensions\NewRelic.Providers.Wrapper.Wcf3.Instrumentation.xml"))) -or
        (!(CheckFileExists("Extensions\NewRelic.Providers.Wrapper.WebApi1.Instrumentation.xml"))) -or
        (!(CheckFileExists("Extensions\NewRelic.Providers.Wrapper.WebApi2.Instrumentation.xml"))) -or
		(!(CheckFileExists("newrelic.config"))) -or
		(!(CheckFileExists("newrelic.xsd")))) {
		return $false
	}
	else {
		return $true
	}
}

Function CheckFileExists([string] $filename)
{	
	If (!(Test-Path $filename)) {
		Write-Host "A required file was not installed: " $filename
		return $false
	}	
	
	return $true
}


Function ValidateRegistryKeys
{
    $result = $true

    $keys =  @( 
        "HKLM:\SOFTWARE\New Relic",
        "HKLM:\SOFTWARE\Wow6432Node\New Relic",
        "HKLM:\SOFTWARE\Classes\CLSID\{71DA0A04-7777-4EC6-9643-7D28B46A8A41}",
        "HKLM:\SOFTWARE\Classes\Wow6432Node\CLSID\{71DA0A04-7777-4EC6-9643-7D28B46A8A41}"
    )

    foreach($key in $keys) {
        if (-Not (Test-Path $key)) {
            Write-Host "Registry: $key does not exist"
            $result =  $false
        }
    }

    return $result
}

Function ValidateRegistryItemProperties
{
	$result = $true
	
	$properties = @{
		"HKLM:\SYSTEM\CurrentControlSet\Services\W3SVC" = "Environment";
		"HKLM:\SYSTEM\CurrentControlSet\Services\WAS" = "Environment"
	}

    foreach($key in $properties.Keys) {
        $value = $properties[$key]
        if (-not (Get-ItemProperty -LiteralPath $key -Name $value -ErrorAction SilentlyContinue)) {
            Write-Host "Registry: $key/$value does not exist"
            $result = $false
			break
        }
    }
	
	return $result
}

Function ValidateRegistryItemPropertyValues
{
	$result = $true
	
	# NewRelicHome property in HKLM Software
	$expectedHome = "C:\ProgramData\New Relic\.NET Agent\"
	$actualHome = Get-ItemProperty -LiteralPath "HKLM:SOFTWARE\New Relic\.NET Agent" | Select-Object -ExpandProperty NewRelicHome
	If ($actualHome -ne $expectedHome) {
		Write-Host "Expected HKLM:SOFTWARE\New Relic\.NET Agent\NewRelicHome to be $expectedHome, but was $actualHome"
		$result = $false
	}

	# NewRelicHome in Wow6432Node
	$actualHome = Get-ItemProperty -LiteralPath "HKLM:SOFTWARE\Wow6432Node\New Relic\.NET Agent" | Select-Object -ExpandProperty NewRelicHome
	If ($actualHome -ne $expectedHome) {
		Write-Host "Expected HKLM:SOFTWARE\Wow6432Node\New Relic\.NET Agent\NewRelicHome to be $expectedHome, but was $actualHome"
		$result = $false
	}
	
	return $result
}

Function ValidateEnvironmentVariables {

    $result = $true
    $EnvironmentVars = @{
        "COR_ENABLE_PROFILING" = $env:COR_ENABLE_PROFILING;
        "COR_PROFILER" = $env:COR_PROFILER
    }

    foreach($key in $EnvironmentVars.Keys) {
        if (([string]::IsNullOrEmpty($EnvironmentVars[$key]))) {
            Write-Host "Environment Variable: $key not found."
            $result = $false
        }
    }

    return $result
}

Function ValidateEmptyRegistryKeys {
    $result = $true

    $keys =  @( 
        "HKLM:\SOFTWARE\New Relic",
        "HKLM:\SOFTWARE\Wow6432Node\New Relic",
        "HKLM:\SOFTWARE\Classes\CLSID\{71DA0A04-7777-4EC6-9643-7D28B46A8A41}",
        "HKLM:\SOFTWARE\Classes\Wow6432Node\CLSID\{71DA0A04-7777-4EC6-9643-7D28B46A8A41}"
    )

    foreach($key in $keys) {
        if (Test-Path $key) {
            Write-Host "Registry: $key exists"
            $result =  $false
        }
    }

    return $result
}

Function ValidateEmptyItemProperties {

    $result = $true
    $PropertiesToRemove = @{
    "HKLM:SYSTEM\CurrentControlSet\Services\W3SVC" = "Environment";
    "HKLM:SYSTEM\CurrentControlSet\Services\WAS" = "Environment"
    }

    foreach($key in $PropertiesToRemove.Keys) {
        $value = $PropertiesToRemove[$key]
        if (Get-ItemProperty -LiteralPath $key -Name $value -ErrorAction SilentlyContinue) {
            Write-Host "Registry: $key/$value exists"
            $result = $false
        }
    }

    return $result
}

Function ValidateEmptyEnvironmentVariables {

    $result = $true
    $EnvironmentVars = @{
        "COR_ENABLE_PROFILING" = $env:COR_ENABLE_PROFILING;
        "COR_PROFILER" = $env:COR_PROFILER;
        "NEWRELIC_INSTALL_PATH" = $env:NEWRELIC_INSTALL_PATH
    }

    foreach($key in $EnvironmentVars.Keys) {
        if (!([string]::IsNullOrEmpty($EnvironmentVars[$key]))) {
            Write-Host "Enviornment Variable: $key exists."
            $result = $false
        }
    }

    return $result
}

Function ValidateEmptyInstallDirectories ([string] $installPath ) {

    $result = $true

    if ($installPath.Contains("Program Files")) {
        if (Test-Path "$env:ProgramFiles\New Relic\.Net Agent") {
            Write-Host "$env:ProgramFiles\New Relic\.Net Agent exists"
            $result =  $false
        }
        if (Test-Path "${env:ProgramFiles(x86)}\New Relic\.Net Agent") {
            Write-Host "${env:ProgramFiles(x86)}\New Relic\.Net Agent exists."
            $result =  $false
        }
    }
    else {
        if (Test-Path "$installPath\New Relic\.Net Agent") {
            Write-Host "$installPath\New Relic\.Net Agent exists."
            $result = $false
        }
    }

    return $result
}

Function ValidateUninstall ([string] $installPath = "") {
    $result = $true

    if ([string]::IsNullOrEmpty($installPath)) {
        $installPath = "$env:ProgramFiles\New Relic\.Net Agent"
    } 

    if (-Not (ValidateEmptyInstallDirectories($installPath))) {
        $result = $false
    }
    if (-Not(ValidateEmptyEnvironmentVariables)) {
        $result = $false
    }
    
    if (-Not(ValidateEmptyRegistryKeys)) {
        $result = $false
    }
    
    if (-Not(ValidateEmptyItemProperties))
    {
        $result = $false
    }

    return $result
}

Function FirstTime_UninstallAndValidate
{
	$CurrentLocation = [string](Get-Location)

	cd C:\AgentThinInstaller
		
    .\uninstall.cmd -Force True
    
    $exitCode = $LastExitCode
    
    If ($exitCode -ne 0){
        Write-Host "FirstTime_UninstallAndValidate: There was an error attempting to uninstall the .NET Agent - Code: $exitCode"
    }
    else{
		If (-Not (ValidateUninstall)) {
			Write-Host "FirstTime_UninstallAndValidate: Uninstall did not pass verification"
			$exitCode = 1
		}
		else {
            Write-Host ""
            Write-Host "---------------------------"
            Write-Host "FirstTime_UninstallAndValidate: Uninstalling - Passed"
            Write-Host "---------------------------"
            Write-Host ""
		}
    }
	
	cd $CurrentLocation
}

$exitCode = 0

# CLR-ec2-i300, CLR-ec2-i350, CLR-ec2-i400, CLR-ec2-i451
#$env:SERVER = "CLR-ec2-i300"

Write-Host ""
Write-Host "---------------------------"
Write-Host "Test Clean Install."
Write-Host "---------------------------"
Write-Host ""


if($env:SERVER -like "CLR-ec2-i300*")
{
    CheckForCleanInstallFailure "3.0"
}
elseif($env:SERVER -like "CLR-ec2-i350*")
{
    CheckForCleanInstallFailure "3.5"
}
elseif($env:SERVER -like "CLR-ec2-i400*")
{
    # FirstTime_UninstallAndValidate
    CheckForCleanInstallSuccess "4.0"
}
elseif($env:SERVER -like "CLR-ec2-i452*")
{
    # FirstTime_UninstallAndValidate
    CheckForCleanInstallSuccess "4.5.2"
}

if($exitCode -ne 0)
{
    exit $exitCode
}

Write-Host ""
Write-Host "---------------------------"
Write-Host "Test Install Same or Newer Agent."
Write-Host "---------------------------"
Write-Host ""

if($env:SERVER -like "CLR-ec2-i400*")
{
    CheckForInstallSameOrNewerVersionSuccess "4.0"
}
elseif($env:SERVER -like "CLR-ec2-i452*")
{
    CheckForInstallSameOrNewerVersionSuccess "4.5.2"
}

Write-Host ""
Write-Host "---------------------------"
Write-Host "Test Install Same or Newer Agent With New LicenceKey and -forceLicenceKey Set"
Write-Host "---------------------------"
Write-Host ""

if($env:SERVER -like "CLR-ec2-i400*")
{
    CheckForInstallSameOrNewerVersionWithNewKeySuccess "4.0"
}
elseif($env:SERVER -like "CLR-ec2-i452*")
{
    CheckForInstallSameOrNewerVersionWithNewKeySuccess "4.5.2"
}

Write-Host ""
Write-Host "---------------------------"
Write-Host "Test Custom Directory Install."
Write-Host "---------------------------"
Write-Host ""

if($env:SERVER -like "CLR-ec2-i400*")
{
    CheckForCustomDirectoryInstallSuccess "4.0"
}
elseif($env:SERVER -like "CLR-ec2-i452*")
{
    CheckForCustomDirectoryInstallSuccess "4.5.2"
}

if($exitCode -ne 0)
{
    exit $exitCode
}