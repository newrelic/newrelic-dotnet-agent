# Install.ps1
#
# Installs the .NET Agent.
# It can also be used to upgrade, downgrade or over-install previous version of
# the .NET Agent that were installed using the scripted installer.

param (
    [string]$licenseKey = "",
    [string]$installPath = $null,
    [switch]$noIisReset = $false,
	[switch]$instrumentAll = $false,
	[switch]$forceLicenseKey = $false
 )

$myPath = Split-Path -Parent -Path $MyInvocation.MyCommand.Definition

$InstallerAgentVersion = "AGENT_VERSION_STRING"

$LogFileDateFormat = "dd-MMM-yyyy_HH.mm.ss.ff" # Filesystem friendly
$LogEntryDateFormat = "MM/dd/yyyy HH:mm:ss.ff"

$Is32Bit =  (Get-WmiObject Win32_OperatingSystem).OSArchitecture -ne "64-bit"
$StagingBase = ".\NewRelicPackage"
$StagingDir = if($Is32Bit) { "$myPath\$StagingBase\NewRelic.Net.Agent\content\newrelic" } else { "$myPath\$StagingBase\NewRelic.Net.Agent.x64\content\newrelic"}
$ProgramFilesX86Dir = "$env:ProgramFiles (x86)\New Relic\.NET Agent\"
$ProgramFilesDir = "$env:ProgramFiles\New Relic\.Net Agent\"
$InstallDir = if ([String]::IsNullOrEmpty($installPath) -eq $True) { $ProgramFilesDir } else { [System.IO.Path]::GetFullPath($installPath) }
$ProgramDataDir = "$env:ProgramData\New Relic\.NET Agent"
$LogsDir = "$env:ProgramData\New Relic\Logs"
$logFileDateStamp = Get-Date -format $LogFileDateFormat
$InstallLogFile = "$env:ProgramData\New Relic\Logs\agent-install.ps1_$logFileDateStamp.txt"
$global:BackupDirName = "$env:ProgramData\New Relic\Backups"
$AgentExists = Test-Path -LiteralPath "HKLM:\SOFTWARE\Classes\CLSID\{71DA0A04-7777-4EC6-9643-7D28B46A8A41}\InprocServer32"

Function HasAdminRights {
	([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator")
}

Function LogEntry ([string]$logentry, [bool]$toconsole, [string]$exceptionMessage = "") {
    if ($logentry -eq "") { return }
    $now = Get-Date -format $LogEntryDateFormat
    if ($toconsole) {
        Write-Host "$now $logentry"
    }
    Add-content "$InstallLogFile" -value "$now $logentry" -ErrorAction Stop
    if (!([string]::IsNullOrEmpty($exceptionMessage))) {
         Write-Host $exceptionMessage
         Add-content "$InstallLogFile" -value "--> $exceptionMessage" -ErrorAction Stop
    }
}

Function GetInstalledVersion ([string] $path) {
	return [System.Diagnostics.FileVersionInfo]::GetVersionInfo("$path\NewRelic.Agent.Core.dll").FileVersion
}

Function GetInstalledPath{
	$pathData = Get-ItemProperty -LiteralPath "HKLM:\SOFTWARE\Classes\CLSID\{71DA0A04-7777-4EC6-9643-7D28B46A8A41}\InprocServer32" -Name "(Default)"
	return Split-Path -Path $pathData.'(default)'
}

# Currently there are only 2 registry keys that need to be added to.
# "HKLM:SYSTEM\CurrentControlSet\Services\W3SVC" | "HKLM:SYSTEM\CurrentControlSet\Services\WAS"
# This is so web applications, by default, will be instrumented
Function AddUpdateRegKey([System.String]$registryPath) {
    $keyName = "Environment"

    $corEnableProfiling = "COR_ENABLE_PROFILING=1"
    $corProfiler = "COR_PROFILER={71DA0A04-7777-4EC6-9643-7D28B46A8A41}"
    $newRelicInstallPath = "NEWRELIC_INSTALL_PATH=" + $InstallDir + "\"

    $value = @($corEnableProfiling, $corProfiler, $newRelicInstallPath)

	try {
		if(!(Test-Path $registryPath)) {
			New-Item -Path $registryPath -Force | Out-Null
			New-ItemProperty -Path $registryPath -Name $keyName -Value $value -PropertyType MultiString -Force | Out-Null
		}
		else {
			New-ItemProperty -Path $registryPath -Name $keyName -Value $value -PropertyType MultiString -Force | Out-Null
		}
	}
	catch {
		LogEntry "ERROR: Failed to register keys, aborting installation." $True $_.Exception.Message
		return -1
	}
}

# Backup Existing Agent 
# Copy the currently installed agent directory
# Returns false on failure
Function BackupAgent {	
	LogEntry "INFO: Backing up previous agent install." $True

	$global:BackupDirName = $global:BackupDirName + "\Backup" +  (Get-Date -f $LogFileDateFormat)

	try {
		Copy-Item $InstallDir -Destination $global:BackupDirName\InstallDir -Recurse
	}
	catch {
		LogEntry "ERROR: Failed backup of current installation directory: [$InstallDir]." $True $_.Exception.Message
		return $false
	}
		try {
		if (Test-Path -Path $ProgramFilesX86Dir) {
			Copy-Item $ProgramFilesX86Dir -Destination $global:BackupDirName\ProgramFilesX86 -Recurse
		}
	}
	catch {
		LogEntry "ERROR: Failed backup of current installation directory: [$ProgramFilesX86Dir]." $True $_.Exception.Message
		return $false
	}

	try {
		Copy-Item $ProgramDataDir -Destination $global:BackupDirName\ProgramData -Recurse
	}
	catch {
		LogEntry "ERROR: Failed backup of current installation directory: [$ProgramDataDir]." $True	$_.Exception.Message
		return $false
	}

	return $true
}

# If it exists, remove staging directory from prior installation; create a new one for this installation
# Returns false of failure
Function CreateStagingDirectory {	
	LogEntry "INFO: Checking for staging directory." $True
	try {
		if (Test-Path -Path $StagingBase) {
			LogEntry "INFO: Removing staging directory." $True
			Remove-Item -Recurse $StagingBase
		}
	}
	catch {
		LogEntry "ERROR: Failed to remove staging directory: [$StagingBase]." $True $_.Exception.Message
		return $False
	}
	
	try {
			LogEntry "INFO: Creating staging directory: [$StagingBase]." $True
			New-Item $StagingBase -ItemType directory | Out-Null
		}
	catch {
		LogEntry "ERROR: Failed to create staging directory, aborting installation: [$StagingBase]." $True $_.Exception.Message
		return $False
	}

	return $True
}

Function CreateInstallDirectory {
	LogEntry "INFO: Creating install and data directories." $True
	try {
		# When we do the work for upgrading we will likely want to make changes to the -Force and -ErroAction values here.
		New-Item $InstallDir -ItemType Directory -Force -ErrorAction Continue | Out-Null

		#Create x86 program files directory for NewRelic if no custom install directory is provided
		if ( [String]::IsNullOrEmpty($installPath) -eq $True) {
			New-Item $ProgramFilesX86Dir -ItemType Directory -Force -ErrorAction Continue | Out-Null
		}
	}
	catch {
		LogEntry "ERROR: Failed to create installation directory: [$InstallDir]." $True $_.Exception.Message
		return $False
	}
	
	return $True
}

Function CreateDataDirectory {
	try {
		New-Item $ProgramDataDir -ItemType Directory -Force -ErrorAction Continue | Out-Null
		New-Item "$LogsDir" -ItemType Directory -Force -ErrorAction Continue | Out-Null
		LogEntry "INFO: Successfully created data directory" $True
	}
	catch {
		LogEntry "ERROR: Failed to create data directory." $True $_.Exception.Message
		return $False
	}
	return $True
}

# Copies agent files to appropriate installation directories
Function CopyAgentFiles {
	LogEntry "INFO: Copying agent files." $True
	try{
		Copy-Item -path "$StagingDir\ProgramFiles\NewRelic\NetAgent\*" -Exclude @("x86") -Destination $InstallDir -Force -Recurse -ErrorAction Continue
	}
	catch{
		LogEntry "ERROR: Failed to copy to installation directory: [$InstallDir]." $True $_.Exception.Message
		return $False
	}

	try {
		# Copying over the 32bit profiler to the correct location
		if ($Is32Bit -ne $True) {
			if ([String]::IsNullOrEmpty($installPath) -ne $True) {
				# Custom Path
				Copy-Item -path "$StagingDir\ProgramFiles\NewRelic\NetAgent\x86" -Destination $InstallDir -Force -Recurse -ErrorAction Continue
			}
			else {
				# Default Path
				Copy-Item -path "$StagingDir\ProgramFiles\NewRelic\NetAgent\x86\*.dll" -Destination $ProgramFilesX86Dir -Force -Recurse -ErrorAction Continue
			}
		}
	}
	catch {
		LogEntry "ERROR: Failed to copy 32bit profiler to target directory." $True $_.Exception.Message
		return $False
	}

	try{
		Copy-Item -path "$StagingDir\ProgramData\NewRelic\NetAgent\*" -Destination $ProgramDataDir -Force -Recurse -ErrorAction Continue
	}
	catch{
		LogEntry "ERROR: Failed to copy Agent files to ProgramData directory: [$ProgramDataDir]." $True $_.Exception.Message
		return $False
	}

	return $True
}

Function RestoreConfigFile {
	LogEntry "INFO: Restoring previous newrelic.config to updated install." $True

	try {
		Copy-Item -path "$global:BackupDirName\ProgramData\newrelic.config" -Destination $ProgramDataDir -Force -ErrorAction Continue
		return $true
	}
	catch {
		LogEntry "ERROR: Unable to restore previous newrelic.config." $True $_.Exception.Message
		return $False
	}
}

Function MsiInstalled{
	$keyBases = @(
		"HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
		"HKLM:\SOFTWARE\Classes\Installer\Products"
	)

	$keyPaths= @()
	try {
		foreach ($keyBase in $keyBases)	{
			if (!(Test-Path -Path $keyBase)) { return $false }
			Get-ChildItem -Path $keyBase | ForEach-Object -Process {
				$leaf = $_ | Split-Path -Leaf
				$data = Get-ItemProperty -LiteralPath "$keyBase\$leaf"
				if($data.DisplayName -like "New Relic .Net Agent*" -Or $data.ProductName -like "New Relic .Net Agent*"){
					$keyPaths += "$keyBase\$leaf"
				}
			}
		}
	}
	catch {
		return $false
	}

	if($keyPaths.Count -gt 0){
		return $true
	}
	return $false
}

Function ExpandNuGetPackage {
	LogEntry "INFO: Expanding NuGet package." $True
	try {
		if ($Is32Bit) {
			if ( Test-Path "NewRelic.Net.Agent.*.nupkg")  {
				& $myPath\nuget.exe install NewRelic.Net.Agent -ExcludeVersion -OutputDirectory $StagingBase
				if ($LASTEXITCODE -ne 0) {
                    LogEntry "Nuget.exe command failed: $myPath\nuget.exe install NewRelic.Net.Agent -ExcludeVersion -OutputDirectory $StagingBase" $True
                    throw
                }
			}
			else {
				LogEntry "ERROR: Unable to find necessary Nuget package." $True
				return $false
			}
			
		}
		else {
			if ( Test-Path "NewRelic.Net.Agent.x64.*.nupkg") {
 				& $myPath\nuget.exe install NewRelic.Net.Agent.x64 -ExcludeVersion -OutputDirectory $StagingBase
				 if ($LASTEXITCODE -ne 0) {
                    LogEntry "Nuget.exe command failed: $myPath\nuget.exe install NewRelic.Net.Agent.x64 -ExcludeVersion -OutputDirectory $StagingBase" $True
                    throw
				 }
			}
			else {
				LogEntry "ERROR: Unable to find necesary Nuget package (x64)." $True
				return $false
			}
		}
	}
	catch {
		LogEntry "ERROR: Failed to extract Nuget packages, aborting installation." $True $_.Exception.Message
		return $False
	}

	return $True
}

Function AddProfilerRegistryKeys {
	try {

		$registryPath = "HKLM:\SOFTWARE\Classes\CLSID\{71DA0A04-7777-4EC6-9643-7D28B46A8A41}"
		$keyName = "(Default)"
		$value = "New Relic .Net Profiler"

		if(!(Test-Path $registryPath)) {
			New-Item -Path $registryPath -Force | Out-Null
		}
		New-ItemProperty -Path $registryPath -Name $keyName -Value $value -PropertyType String -Force | Out-Null
		
		$registryPath = "HKLM:\SOFTWARE\Classes\CLSID\{71DA0A04-7777-4EC6-9643-7D28B46A8A41}\InProcServer32"
		$keyName = "(Default)"
		$value = "$InstallDir\NewRelic.Profiler.dll"
		$keyname2 = "ThreadingModel"
		$value2 = "Both"

		if(!(Test-Path $registryPath)) {
			New-Item -Path $registryPath -Force | Out-Null
		}
		New-ItemProperty -Path $registryPath -Name $keyName -Value $value -PropertyType String -Force | Out-Null
		New-ItemProperty -Path $registryPath -Name $keyName2 -Value $value2 -PropertyType String -Force | Out-Null

		$registryPath = "HKLM:\SOFTWARE\Classes\CLSID\{71DA0A04-7777-4EC6-9643-7D28B46A8A41}\Version"
		$keyName = "(Default)"
		$value = "$InstallerAgentVersion"

		if(!(Test-Path $registryPath)) {
			New-Item -Path $registryPath -Force | Out-Null
		}
		New-ItemProperty -Path $registryPath -Name $keyName -Value $value -PropertyType String -Force | Out-Null

		if ($is32Bit -ne $True) {
		
			$registryPath = "HKLM:\SOFTWARE\Classes\WOW6432Node\CLSID\{71DA0A04-7777-4EC6-9643-7D28B46A8A41}"
			$keyName = "(Default)"
			$value = "New Relic .Net Profiler"

			if(!(Test-Path $registryPath)) {
				New-Item -Path $registryPath -Force | Out-Null
			}

			New-ItemProperty -Path $registryPath -Name $keyName -Value $value -PropertyType String -Force | Out-Null
			
			$registryPath = "HKLM:\SOFTWARE\Classes\WOW6432Node\CLSID\{71DA0A04-7777-4EC6-9643-7D28B46A8A41}\InprocServer32"
			$keyName = "(Default)"

			if ([String]::IsNullOrEmpty($InstallPath) -ne $True) {
				$value = "$InstallDir\x86\NewRelic.Profiler.dll"
			}
			else {
				$value = "$ProgramFilesX86Dir\NewRelic.Profiler.dll"
			}
			
			$keyname2 = "ThreadingModel"
			$value2 = "Both"

			if(!(Test-Path $registryPath)) {
				New-Item -Path $registryPath -Force | Out-Null
			}
			New-ItemProperty -Path $registryPath -Name $keyName -Value $value -PropertyType String -Force | Out-Null
			New-ItemProperty -Path $registryPath -Name $keyName2 -Value $value2 -PropertyType String -Force | Out-Null

			$registryPath = "HKLM:\SOFTWARE\Classes\WOW6432Node\CLSID\{71DA0A04-7777-4EC6-9643-7D28B46A8A41}\Version"
			$keyName = "(Default)"
			$value = "$InstallerAgentVersion"

			if(!(Test-Path $registryPath)) {
				New-Item -Path $registryPath -Force | Out-Null
			}
			New-ItemProperty -Path $registryPath -Name $keyName -Value $value -PropertyType String -Force | Out-Null
		}
	}
	catch {
		LogEntry "ERROR: Failed to add profiler registry keys." $True $_.Exception.Message
		throw 
	}
}

Function AddNewRelicHomeRegistryKeys {
	LogEntry "INFO: Adding NewRelicHome registry keys." $True
	try {
		$registryPath = "HKLM:\SOFTWARE\New Relic\.Net Agent"
		$keyName = "NewRelicHome"
		$value = $ProgramDataDir + "\"
		
		if(!(Test-Path $registryPath)) {
			New-Item -Path $registryPath -Force | Out-Null
		}

		New-ItemProperty -Path $registryPath -Name $keyName -Value $value -PropertyType String -Force | Out-Null

		if ($Is32Bit -ne $True) {

			$registryPath = "HKLM:\SOFTWARE\Wow6432Node\New Relic\.Net Agent";
			
			if(!(Test-Path $registryPath)) {
					New-Item -Path $registryPath -Force | Out-Null
			}

			New-ItemProperty -Path $registryPath -Name $keyName -Value $value -PropertyType String -Force | Out-Null
		}
	}
	catch {
		LogEntry "ERROR: Unable to set NewRelicHome registry keys." $True $_.Exception.Message
		throw
	}
}

Function AddRegistryKeys {
	LogEntry "INFO: Adding registry keys." $True

	try {
		$regKeys = @("HKLM:SYSTEM\CurrentControlSet\Services\W3SVC","HKLM:SYSTEM\CurrentControlSet\Services\WAS")
		foreach ($key in $regKeys) 
		{ 
			if (AddUpdateRegKey $key -eq -1) {
				LogEntry "ERROR: Failed to register key: [$key], aborting installation." $True
				return $False
			}
		}	

		AddNewRelicHomeRegistryKeys
		AddProfilerRegistryKeys

		return $True
	} 
	catch {
		LogEntry "ERROR: Failed to add registry keys." $True $_.Exception.Message
		return $False
	}
}

Function ConfigureDirectoryPermissions {
	LogEntry "INFO: Configuring directory permissions." $True
	#IIS_IUSRS need access to this folder to get assemblies and to write logs
	try {
		#Changed from Get-Acl since Get-Acl captures the entire ACL including the owner data.
		#Unless your user has certain permissions this "complete" object cannot be used to update the ACL in using Set-Acl
		#GetAccessControl pulls in just the access specific data as specified in the method.
		#This means that Set-Acl only has to replace a portion of the ACL, or in our case just add a tiny bit to it.
		#See this SO post for more details: http://stackoverflow.com/questions/6622124/why-does-set-acl-on-the-drive-root-try-to-set-ownership-of-the-object
		$acl = (Get-Item $InstallDir).GetAccessControl('Access')
		$rule = New-Object System.Security.AccessControl.FileSystemAccessRule("IIS_IUSRS","FullControl", "ContainerInherit, ObjectInherit", "None", "Allow")
		$acl.AddAccessRule($rule)
		Set-Acl $InstallDir $acl
	}
	catch {
		LogEntry "ERROR: Unable to configure directory permissions." $True $_.Exception.Message
		return $False
	}

	return $True
}

Function CheckDotNetVersions {
	try {
		Get-ChildItem 'HKLM:\SOFTWARE\Microsoft\NET Framework Setup\NDP' -recurse |
		Get-ItemProperty -name Version,Release -EA 0 | Where { $_.PSChildName -match '^(?!S)\p{L}'} | Select PSChildName, Version, Release -OutVariable results | Out-Null

		$versions = [System.Collections.ArrayList]@()

		foreach($result in $results) {
			$versionParts = ([System.String]$result.Version).Split('.')
			$versions.Add($versionParts[0] + "." + $versionParts[1]) | Out-Null
		}

		if (-Not($versions.Contains("4.0"))){
			LogEntry "ERROR: The installer requires .NET 4.0 to be installed." $True
			return $False
		}

		if (($versions.Contains("2.0") -or $versions.Contains("3.0")) -and (-Not ($versions.Contains("3.5")))) {
			LogEntry "INFO: You do not appear to have .NET 3.5 installed. You will not be able to instrument your .NET 2.0/3.0 applications until .NET 3.5 is installed." $True
		}
	}
	catch {
		LogEntry $_.Exception.exceptionMessage $True
		LogEntry "INFO: We were unable to determine which versions of .NET you current have installed." $True $_.Exception.Message
	}
	return $True
}

# Compare Agent versions, assumes format is always: ###.###.###.###
# Returns 0 if versions are equal, 1 if installer > installed, -1 if installer < installed
Function CompareVersions ([string]$installedVersion = "", [string]$installerVersion = "0") {
	
	$installerArray = $installerVersion.Split('.', 4)
	$installedArray = $installedVersion.Split('.', 4)

	$index = 0
	$max = 4	
	while (($installedArray[$index] -eq $installerArray[$index]) -and ($index -lt $max)) {
		$index = $index + 1
	}
	if ($index -lt $max) {
		$diff = $installerArray[$index] - $installedArray[$index]
		if ($diff -lt 0) { 
			#Logentry "INFO: Installed version [$installedVersion] is newer than installer version [$installerVersion]." $True
			return -1 
		} 
		if ($diff -gt 0) {
			# no exceptionMessage, this is the 'normal' case
			return 1 
		}
	}
	#LogEntry "INFO: Installed version [$installedVersion] is the same as installer version [$installerVersion]." $True
	return 0
}

Function NewerVersionInstalled{
	$compareResult = CompareVersions -installedVersion $AgentVersionInstalled -installerVersion $InstallerAgentVersion 
	if ( $compareResult  -eq -1) {
		return $True
	}
	return $False
}

Function SameVersionInstalled{
	$compareResult = CompareVersions -installedVersion $AgentVersionInstalled -installerVersion $InstallerAgentVersion 
	if ( $compareResult -eq 0) {
		return $True
	}
	return $False
}

Function OlderVersionInstalled{
	$compareResult = CompareVersions -installedVersion $AgentVersionInstalled -installerVersion $InstallerAgentVersion 
	if ( $compareResult -eq 1) {
		return $True
	}
	return $False
}

Function UpdateConfigFile ([System.String] $configPath) {
	LogEntry "INFO: Updating config file with license key and log directory information." $True
	# Set license key and log directory 
	try {
		$xdoc = new-object System.Xml.XmlDocument
		$file = resolve-path("$configPath")
		$xdoc.load($file)
		$xdoc.configuration.service.licenseKey = [System.String]$licenseKey
		$xdoc.Save($file)
	}
	catch {
		LogEntry "ERROR: Unable to update newrelic.config file with license key and log directory." $True $_.Exception.Message
		return $False
	}
    
	return $True
}

Function GetLicenseKeyFromConfigFile {
	LogEntry "INFO: Getting license key from existing newrelic.config file." $True
	try {
		$xdoc = new-object System.Xml.XmlDocument
		$file = resolve-path("$ProgramDataDir\newrelic.config")
		$xdoc.load($file)
		$foundLicenseKey = $xdoc.configuration.service.licenseKey
		return $foundLicenseKey
	}
	catch {
		LogEntry "ERROR: Unable to get license key from existing config file." $True $_.Exception.Message
		return ""
	}
}

Function CheckResetIIS {
	try {
		if (-not ($noIisReset)) {
			LogEntry "INFO: Performing IISReset." $True
			IISRESET | Out-Null
		}
		else {
			LogEntry "INFO: IISReset not performed.  This will need to be done before you are able instrument your web applications." $True
		}
	}
	catch {
		LogEntry "ERROR: Unable to reset IIS." $True $_.Exception.Message
		return $False
	}

	return $True
}

Function RollBackCleanInstall {
	LogEntry "ERROR: Installation failed. Rolling back changes." $True
	& $myPath\uninstall.cmd -Rollback
}

Function HandleMSIInstalled {
	LogEntry "INFO: A version of the New Relic agent was previously installed on this machine using the MSI installer. Please use the MSI to remove this installation before attempting to install via the scripted installer." $True
	Exit 1
}

Function EnvVarsSet {
	if ((-Not ([String]::IsNullOrEmpty([System.Environment]::GetEnvironmentVariable("COR_ENABLE_PROFILING", "Machine")))) -and 
		(-Not ([String]::IsNullOrEmpty([System.Environment]::GetEnvironmentVariable("COR_PROFILER", "Machine")))) -and 
		(-Not ([String]::IsNullOrEmpty([System.Environment]::GetEnvironmentVariable("NEWRELIC_INSTALL_PATH", "Machine"))))) {
		return $true
	}

	return $false
}

Function ClearEnvironmentVariables {
	LogEntry "INFO: Clearing Environment Variables to disable instrumentAll." $True
	try {
		[Environment]::SetEnvironmentVariable("COR_ENABLE_PROFILING","", "Machine")
		[Environment]::SetEnvironmentVariable("COR_PROFILER","", "Machine")
		[Environment]::SetEnvironmentVariable("NEWRELIC_INSTALL_PATH","", "Machine")
		return $True
	}
	catch {
		LogEntry "ERROR: Failed to clear environment variables." $True $_.Exception.Message
		return $False
	}
}

Function SetEnvironmentVariables {
	LogEntry "INFO: Adding Environment Variables to enable instrumentAll." $True
	try {
		[Environment]::SetEnvironmentVariable("COR_ENABLE_PROFILING","1", "Machine")
		[Environment]::SetEnvironmentVariable("COR_PROFILER","{71DA0A04-7777-4EC6-9643-7D28B46A8A41}", "Machine")
		[Environment]::SetEnvironmentVariable("NEWRELIC_INSTALL_PATH","$InstallDir", "Machine")
		return $True
	}
	catch {
		LogEntry "ERROR: Failed to add environment variables." $True $_.Exception.Message
		return $False
	}
}

# Parameter $set:  boolean, indicating set or clear environment variables
Function ModifyEnvironmentVariables ([System.Boolean]$set = $False) {
	if (-not ($set)) {
		$clearEnvironmentVariablesStatus = ClearEnvironmentVariables
		return $clearEnvironmentVariablesStatus
	}
	else {
		$setEnvironmentVariablesStatus = SetEnvironmentVariables
		return $setEnvironmentVariablesStatus
	}
}

# Called from HandleUpgradeInstall if something goes wrong
# Assumes $global:BackupDirName is set and GetInstalledPath returns the right location
Function RestoreAgentFilesFromBackup {
	$restoreToPath = GetInstalledPath
	LogEntry "INFO: Restoring agent files from backup [$global:BackupDirName] to [$restoreToPath]." $True
	try{
		# Restore installation directory (e.g. Program Files)
		Copy-Item -path "$global:BackupDirName\InstallDir" -Exclude @("x86") -Destination $InstallDir -Force -Recurse -ErrorAction Continue
	}
	catch{
		LogEntry "ERROR: Failed to restore [$global:BackupDirName\InstallDir] to installation directory [$InstallDir]." $True $_.Exception.Message
		return $False
	}

	try {
		# Restore 32bit profiler 
		if ($Is32Bit -ne $True) {
			if ([String]::IsNullOrEmpty($installPath) -eq $True) {
				# Default Path
				Copy-Item -path "$global:BackupDirName\ProgramFilesX86" -Destination $ProgramFilesX86Dir -Force -Recurse -ErrorAction Continue
			}
		}
	}
	catch {
		LogEntry "ERROR: Failed to restore 32bit profiler from backup directory [$global:BackupDirName\InstallDir\x86] to [$ProgramFilesX86Dir]." $True $_.Exception.Message
		return $False
	}

	try{
		# Restore ProgramData
		Copy-Item -path "$global:BackupDirName\ProgramData" -Destination $ProgramDataDir -Force -Recurse -ErrorAction Continue
	}
	catch{
		LogEntry "ERROR: Failed to restore [$global:BackupDirName\ProgramData] to program data directory [$ProgramDataDir]." $True $_.Exception.Message
		return $False
	}

	return $True
}

Function HandleUpgradeInstall {

	$NewerVersionInstallStatus = NewerVersionInstalled
	if ($NewerVersionInstallStatus -eq $True) {
		LogEntry "WARNING: Installing OLDER version [$InstallerAgentVersion] over current version [$AgentVersionInstalled]." $True
	}
	else {
		$SameVersionInstalledStatus = SameVersionInstalled
		if ($SameVersionInstalledStatus -eq $True) {
			LogEntry "WARNING: Installing SAME version as currently installed [$InstallerAgentVersion]." $True
		}
		else {
			LogEntry "INFO: Upgrading currently installed version [$AgentVersionInstalled] of the New Relic Agent with version [$InstallerAgentVersion]." $True
		}
	} 

	# Make sure that the install path for the current install is the same as the path for the previous install
	$currentInstalledPath = GetInstalledPath
	if ($currentInstalledPath -ne $installDir) {
		LogEntry "ERROR: You are attempting to upgrade the agent to a new directory. Please uninstall the current agent first.  If you wish to save your newrelic.config customizations be sure to back it up first before you uninstall. It can be found in $ProgramDataDir." $True
		return $False
	}

	# Check to see if the provided license key differs from the one in the current newrelic.config file.
	$key = GetLicenseKeyFromConfigFile
	if ($key -ne $licenseKey) {
		if (-Not($forceLicenseKey)) {
			LogEntry "ERROR: The license key you have entered differs from the one used in the previous install. If you wish to overwrite the license key please re-run this installer with the -forceLicenceKey flag." $True
			return $False
		}
		else {
			LogEntry "INFO: The license key you have entered differs from the one used in the previous install. Because you used -ForceLicenseKey we will update this key as part of the install process." $True
		}
	}

	# Backup current installation
	$backupAgentStatus = BackupAgent
	if ($backupAgentStatus -eq $False) {
	    return $False
	}

	# Check instrumentAll
	$isPreInstallEnvVarsSet = EnvVarsSet
	ClearEnvironmentVariables | Out-Null
	if ($instrumentAll.IsPresent) { SetEnvironmentVariables }


	# Unpack files into staging dir
	if (-Not ((CreateStagingDirectory -eq $True) -and
			  (ExpandNuGetPackage -eq $True))) {
		return $False
	}

	# Copy Agent files to installation dir
	$copyAgentFilesStatus = CopyAgentFiles
	if ($copyAgentFilesStatus -eq $False) {
		# rollback here, restore backed up files to installation directories AND restore env vars
		RestoreAgentFilesFromBackup
		if ($isPreInstallEnvVarsSet) { SetEnvironmentVariables }
		else { ClearEnvironmentVariables }
		return $False
	}

	# Restore the previous config file to the updated install to maintain any customizations that were previously made.
	$restoreConfigFileStatus = RestoreConfigFile
	if ($restoreConfigFileStatus -ne $True) {
		return $False
	}

	# updating license key if necessary
	if (($key -ne $licenseKey) -and ($forceLicenseKey)) {
		$updateConfigFileSuccess = UpdateConfigFile("$ProgramDataDir\newrelic.config")
		if ($updateConfigFileSuccess -ne $True) {
			LogEntry "WARNING: We were unable to update the license key as part of this install.  You can manually add the new license key to the newrelic.config file located at $ProgramDataDir." $True
		}
	}

	# Reset IIS 
	$checkResetIISStatus = CheckResetIIS
	if ($checkResetIISStatuss -eq $False) {
		return $False
	}

	return $True
}

Function HandleCleanInstall {
	LogEntry "INFO: Performing a clean install of the agent." $True

	# Attempt to perform install.  If any step fails then we roll back the install
	$boolInstrumentAll = $instrumentAll.IsPresent
	if (-Not ((CreateStagingDirectory -eq $True) -and
			  (ExpandNuGetPackage -eq $True) -and
			  (UpdateConfigFile("$StagingDir\ProgramData\NewRelic\NetAgent\newrelic.config") -eq $True) -and
			  (CreateInstallDirectory -eq $True) -and
			  (AddRegistryKeys -eq $True) -and
			  (ConfigureDirectoryPermissions -eq $True) -and
			  (CopyAgentFiles -eq $True) -and
			  ((ModifyEnvironmentVariables -set $boolInstrumentAll) -eq $True))){
		
		RollBackCleanInstall
	
		return $False
	}

	$checkResetIISStatus = CheckResetIIS
	if ($checkResetIISStatuss -eq $False) {
		return $False
	}

	return $True
}

#
# Execution starts here
#

if (-Not(HasAdminRights)) {
	Write-Host "ERROR: You must have administrator rights to run this installer."
	Start-Sleep 1
	exit 1
}

# IMPORTANT: This must be run first for logging to work as the LogEntry method logs here
$CreateDataDirectoryStatus = CreateDataDirectory
if ( $CreateDataDirectoryStatus -ne $True) {
	Start-Sleep 2
	exit 1
}

# Make sure the correct versions of .Net are present
$CheckDotNetVersionsStatus = CheckDotNetVersions
if ($CheckDotNetVersionsStatus -ne $True){
	Start-Sleep 2
	exit 1
}

# If agent exists we go down the the upgrade path for upgrades, downgrades and over-installs
if ($AgentExists -eq $True) {

	$PathToAgent = GetInstalledPath
	
	$msiInstalledStatus = MsiInstalled
	if($msiInstalledStatus -eq $True) {
		HandleMsiInstalled
		Start-Sleep 2
		exit 1
	}

	$AgentVersionInstalled = GetInstalledVersion -path "$PathToAgent"

	$HandleUpgradeInstallStatus = HandleUpgradeInstall
	if ($HandleUpgradeInstallStatus -ne $True) {
		LogEntry "ERROR: Failed to successfully upgrade the agent." $True
		Start-Sleep 2
		exit 1
	}
	else {
	LogEntry "INFO: Agent successfully upgraded." $True
	Start-Sleep 2
	exit 0
	}
}
else {
	# The agent isn't already installed so perform a clean install.
	$HandleCleanInstallStatus = HandleCleanInstall
	if ($HandleCleanInstallStatus -ne $True) {
		LogEntry "ERROR: Failed to successfully install agent." $True
		Start-Sleep 2
		exit 1
	}
	else {
		LogEntry "INFO: Agent successfully installed." $True
		Start-Sleep 2
		exit 0
	}
}

Start-Sleep 2
exit 0
