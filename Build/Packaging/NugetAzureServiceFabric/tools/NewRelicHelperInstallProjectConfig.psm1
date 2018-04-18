function Set-NewRelicAgentEnabled ([System.Xml.XmlElement]$node){
    $node.SetAttribute('value','true')
    $node
}


#Modify the [web|app].config so that we can set NewRelic.AgentEnabled to true
function Update-ProjectConfigFile ([System.__ComObject] $project){
    Try{
        $config = $project.ProjectItems.Item("Web.Config")
    }Catch{
        #Swallow - non webrole project 
    }
    if($config -eq $null){
        $config = $project.ProjectItems.Item("App.Config")
    }
    
    $configPath = $config.Properties.Item("LocalPath").Value
    [xml] $configXml = Get-Content $configPath

    if($configXml -ne $null){
        $newRelicAppSetting = $null
        if(!$configXml.configuration.appSettings.IsEmpty -and $configXml.configuration.appSettings.HasChildNodes){
            $newRelicAppSetting = $configXml.configuration.appSettings.SelectSingleNode("//add[@key = 'NewRelic.AgentEnabled']")
        }

        if($newRelicAppSetting -ne $null){
            Set-NewRelicAgentEnabled $newRelicAppSetting
        }
        else{
            #add the node
            $addSettingNode = $configXml.CreateElement('add')
            $addSettingNode.SetAttribute('key','NewRelic.AgentEnabled')
            Set-NewRelicAgentEnabled $addSettingNode
            
            if($configXml.configuration.appSettings -eq $null){
                $addAppSettingsNode = $configXml.CreateElement('appSettings')
                $configXml.configuration.appendchild($addAppSettingsNode)
            }
            
            $configXml.configuration["appSettings"].appendchild($addSettingNode)
        }
        
        $configXml.Save($configPath);
    }
}
