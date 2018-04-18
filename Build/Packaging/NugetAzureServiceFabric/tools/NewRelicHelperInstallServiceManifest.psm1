#Check for an existing env var and create if missing or if override set update value
function Set-EnvironmentVariable ([String] $envName, [String] $envValue, [System.Xml.XmlElement] $envParent, [bool] $updateValue){
    $nodeExists = $false
    foreach ($i in $envParent.ChildNodes){
        if ($i.Name -eq $envName){
            $nodeExists = $true

            if($updateValue){
                $i.Value = $envValue
            }
        }
    }

    if(!$nodeExists){
        $envVar = $xml.CreateElement('EnvironmentVariable','http://schemas.microsoft.com/2011/01/fabric')
        $envVar.SetAttribute('Name', $envName)
        $envVar.SetAttribute('Value', $envValue)
        $envParent.AppendChild($envVar)
    }
}


#Modify ServiceManifest.xml to add New Relice env vars
function Update-ServiceManifest ([System.__ComObject] $project){
    $pn = $project.Name.ToString()
    
    #$ svcConfigFiles = $DTE.Solution.Projects | Select-Object -Expand ProjectItems.Item("PackageRoot").ProjectItems | Where-Object{$_.Name -like 'ServiceManifest.xml'}
    $svcConfigFile = $project.ProjectItems.Item("PackageRoot").ProjectItems.Item("ServiceManifest.xml")

    if($svcConfigFile -eq $null){
        Write-Host "Unable to find any ServiceManifest.xml files in your solution, please make sure your solution contains an Azure deployment project and try again."
        return
    }

    $ServiceConfig = $svcConfigFile.Properties.Item("FullPath").Value
    if(!(Test-Path $ServiceConfig)) {
        Write-Host "Unable to locate the ServiceManifest.xml file on your filesystem.  Please verify that the ServiceManifest.xml in your solution points to a valid file and try again."
        return
    }

    [xml] $xml = Get-Content $ServiceConfig
    $smPkgName = $xml.ServiceManifest.GetAttribute('Name')
    $smCodePkgName = $xml.ServiceManifest.CodePackage.GetAttribute('Name')
    $smCodePkgVer = $xml.ServiceManifest.CodePackage.GetAttribute('Version')

    if($smPkgName -ccontains $pn){
        Write-Host "Name in ServiceManifest.xml ($smPkgName) does not match Project name ($pn)."
        return
    }

    $ns = new-object Xml.XmlNamespaceManager $xml.NameTable
    $ns.AddNamespace('ns', 'http://schemas.microsoft.com/2011/01/fabric')
    $selectedEnvVarsNode = $xml.SelectSingleNode("/ns:ServiceManifest/ns:CodePackage/ns:EnvironmentVariables", $ns)

    if ($selectedEnvVarsNode -eq $null){
        $envVarsNode = $xml.CreateElement('EnvironmentVariables','http://schemas.microsoft.com/2011/01/fabric')

        $envVarCorEnableProfiling = $xml.CreateElement('EnvironmentVariable','http://schemas.microsoft.com/2011/01/fabric')
        $envVarCorEnableProfiling.SetAttribute('Name', 'COR_ENABLE_PROFILING')
        $envVarCorEnableProfiling.SetAttribute('Value', '1')
        $envVarsNode.AppendChild($envVarCorEnableProfiling)

        $envVarCorProfiler = $xml.CreateElement('EnvironmentVariable','http://schemas.microsoft.com/2011/01/fabric')
        $envVarCorProfiler.SetAttribute('Name', 'COR_PROFILER')
        $envVarCorProfiler.SetAttribute('Value', '{71DA0A04-7777-4EC6-9643-7D28B46A8A41}')
        $envVarsNode.AppendChild($envVarCorProfiler)

        $envVarNRHome = $xml.CreateElement('EnvironmentVariable','http://schemas.microsoft.com/2011/01/fabric')
        $envVarNRHome.SetAttribute('Name', 'NEWRELIC_HOME')
        $envVarNRHome.SetAttribute('Value', "..\$smPkgName.$smCodePkgName.$smCodePkgVer\newrelic")
        $envVarsNode.AppendChild($envVarNRHome)

        $envVarCorProfilerPath = $xml.CreateElement('EnvironmentVariable','http://schemas.microsoft.com/2011/01/fabric')
        $envVarCorProfilerPath.SetAttribute('Name', 'COR_PROFILER_PATH')
        $envVarCorProfilerPath.SetAttribute('Value', "..\$smPkgName.$smCodePkgName.$smCodePkgVer\newrelic\NewRelic.Profiler.dll")
        $envVarsNode.AppendChild($envVarCorProfilerPath)

        $envVarNRInstallPath = $xml.CreateElement('EnvironmentVariable','http://schemas.microsoft.com/2011/01/fabric')
        $envVarNRInstallPath.SetAttribute('Name', 'NEW_RELIC_INSTALL_PATH')
        $envVarNRInstallPath.SetAttribute('Value', "..\$smPkgName.$smCodePkgName.$smCodePkgVer\newrelic")
        $envVarsNode.AppendChild($envVarNRInstallPath)

        if ($NR_LICENSEKEY -eq $null) {
            $NR_LICENSEKEY = Open-InputDialog "New Relic License Key for $pn" "Please enter your New Relic License Key for $pn below:"
        }
        
        if(-not ([String]::IsNullOrWhiteSpace($NR_LICENSEKEY))){
            $envVarNRLicenseKey= $xml.CreateElement('EnvironmentVariable','http://schemas.microsoft.com/2011/01/fabric')
            $envVarNRLicenseKey.SetAttribute('Name', 'NEW_RELIC_LICENSE_KEY')
            $envVarNRLicenseKey.SetAttribute('Value', $NR_LICENSEKEY)
            $envVarsNode.AppendChild($envVarNRLicenseKey)
        }
        
        if ($NR_APPNAME -eq $null) {
            $NR_APPNAME = Open-InputDialog "Name for $pn" "Please enter an app name for $pn" "(optional, if none is provided we will use the solution name)"
        }
        
        if(-not ([String]::IsNullOrWhiteSpace($NR_APPNAME))){
            $envVarNRAppName= $xml.CreateElement('EnvironmentVariable','http://schemas.microsoft.com/2011/01/fabric')
            $envVarNRAppName.SetAttribute('Name', 'NEW_RELIC_APP_NAME')
            $envVarNRAppName.SetAttribute('Value', $NR_APPNAME)
            $envVarsNode.AppendChild($envVarNRAppName)
        }

        $selectedCodePackageNode = $xml.SelectSingleNode("/ns:ServiceManifest/ns:CodePackage", $ns)
        $selectedCodePackageNode.AppendChild($envVarsNode)
    }
    else{
        Set-EnvironmentVariable "COR_ENABLE_PROFILING" "1" $selectedEnvVarsNode $false
        Set-EnvironmentVariable "COR_PROFILER" "{71DA0A04-7777-4EC6-9643-7D28B46A8A41}" $selectedEnvVarsNode $false
        Set-EnvironmentVariable "NEWRELIC_HOME" "..\$smPkgName.$smCodePkgName.$smCodePkgVer\newrelic" $selectedEnvVarsNode $true
        Set-EnvironmentVariable "COR_PROFILER_PATH" "..\$smPkgName.$smCodePkgName.$smCodePkgVer\newrelic\NewRelic.Profiler.dll" $selectedEnvVarsNode $true
        Set-EnvironmentVariable "NEW_RELIC_INSTALL_PATH" "..\$smPkgName.$smCodePkgName.$smCodePkgVer\newrelic" $selectedEnvVarsNode $true

        $licenseKeyNodeExists = $false
        foreach ($i in $selectedEnvVarsNode.ChildNodes){
            if ($i.Name -eq "NEW_RELIC_LICENSE_KEY"){
                $licenseKeyNodeExists = $true
            }
        }
        if(!$licenseKeyNodeExists){
            if ($NR_LICENSEKEY -eq $null) {
                $NR_LICENSEKEY = Open-InputDialog "New Relic License Key for $pn" "Please enter your New Relic License Key for $pn below:"
            }
        
            if(-not ([String]::IsNullOrWhiteSpace($NR_LICENSEKEY))){
                $envVarNRLicenseKey= $xml.CreateElement('EnvironmentVariable','http://schemas.microsoft.com/2011/01/fabric')
                $envVarNRLicenseKey.SetAttribute('Name', 'NEW_RELIC_LICENSE_KEY')
                $envVarNRLicenseKey.SetAttribute('Value', $NR_LICENSEKEY)
                $selectedEnvVarsNode.AppendChild($envVarNRLicenseKey)
            }
        }

        $appNameNodeExists = $false
        foreach ($i in $selectedEnvVarsNode.ChildNodes){
            if ($i.Name -eq "NEW_RELIC_APP_NAME"){
                $appNameNodeExists = $true
            }
        }

        if(!$appNameNodeExists){
            if ($NR_APPNAME -eq $null) {
                $NR_APPNAME = Open-InputDialog "Name for $pn" "Please enter an app name for $pn" "(optional, if none is provided we will use the solution name)"
            }
            
            if(-not ([String]::IsNullOrWhiteSpace($NR_APPNAME))){
                $envVarNRAppName= $xml.CreateElement('EnvironmentVariable','http://schemas.microsoft.com/2011/01/fabric')
                $envVarNRAppName.SetAttribute('Name', 'NEW_RELIC_APP_NAME')
                $envVarNRAppName.SetAttribute('Value', $NR_APPNAME)
                $selectedEnvVarsNode.AppendChild($envVarNRAppName)
            }
        }
    }

    $xml.Save($ServiceConfig);
}
