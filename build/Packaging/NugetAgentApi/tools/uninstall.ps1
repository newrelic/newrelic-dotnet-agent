param($installPath, $toolsPath, $package, $project)

$config = $project.ProjectItems.Item("Web.Config")
$configPath = $config.Properties.Item("LocalPath").Value
[xml] $configXml = gc $configPath

if($configXml -ne $null){
	$newRelicAppSetting = $configXml.configuration.appSettings.SelectSingleNode("//add[@key = 'NewRelic.AppName']")
	if($newRelicAppSetting -ne $null){
		[Void]$newRelicAppSetting.ParentNode.RemoveChild($newRelicAppSetting)
		$configXml.Save($configPath)
	}
}