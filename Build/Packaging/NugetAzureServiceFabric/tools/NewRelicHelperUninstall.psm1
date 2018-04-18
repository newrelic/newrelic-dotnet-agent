# Remove the env var values added by the nuget package in the ServiceManifest.xml.
function Remove-ServiceManifestConfig ([System.__ComObject] $project){
    $svcConfigFile = $project.ProjectItems.Item("PackageRoot").ProjectItems.Item("ServiceManifest.xml")
    $ServiceConfig = $svcConfigFile.Properties.Item("FullPath").Value
    if(!(Test-Path $ServiceConfig)) {
        Write-Host "Unable to locate the ServiceManifest.xml file on your filesystem.  Please verify that the ServiceManifest.xml in your solution points to a valid file and try again."
        return
    }

    [xml] $xml = Get-Content $ServiceConfig
    $ns = new-object Xml.XmlNamespaceManager $xml.NameTable
    $ns.AddNamespace('ns', 'http://schemas.microsoft.com/2011/01/fabric')
    $selectedEnvVarsNode = $xml.SelectSingleNode("/ns:ServiceManifest/ns:CodePackage/ns:EnvironmentVariables", $ns)

    $nodesToRemove = New-Object System.Collections.ArrayList
    foreach ($envVar in $selectedEnvVarsNode.ChildNodes){
        if ($envVar.Name.StartsWith("NEW_RELIC_") -or $envVar.Name.StartsWith("NEWRELIC_") -or $envVar.Name.StartsWith("COR_")){
            $null = $nodesToRemove.Add($envVar)
        }
    }
    
    foreach ($node in $nodesToRemove ){
        $null = $selectedEnvVarsNode.RemoveChild($node)
    }

    $xml.Save($ServiceConfig)
}


#Remove NewRelic.AgentEnabled from the [web|app].config
function Remove-ProjectConfig ([System.__ComObject] $project){
    try{
        $config = $project.ProjectItems.Item("Web.Config")
    }
    catch{
        #Swallow - non webrole project 
    }

    if($config -eq $null){
        $config = $project.ProjectItems.Item("App.Config")
    }

    $configPath = $config.Properties.Item("LocalPath").Value
    if((Test-Path $configPath)) {
        [xml] $configXml = Get-Content $configPath
        if($configXml -ne $null){	
            $newRelicAppSetting = $configXml.configuration.appSettings.SelectSingleNode("//add[@key = 'NewRelic.AgentEnabled']")
            if($newRelicAppSetting -ne $null){
                [Void]$newRelicAppSetting.ParentNode.RemoveChild($newRelicAppSetting)
                $configXml.Save($configPath)
            }
        }
    }
}
