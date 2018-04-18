#Modify New Relic files so that they will be marked as Content and copy always
function Update-NewRelicProjectItems ([System.__ComObject] $project){
    $newrelicDirItems = $project.ProjectItems.Item("newrelic").ProjectItems
    $extensionsDirItems = $newrelicDirItems.Item("Extensions").ProjectItems
    $logsDirItems = $newrelicDirItems.Item("Logs").ProjectItems

    foreach ($newrelicDirItem in $newrelicDirItems) {
        try{
            $null = $newrelicDirItem.Properties.Item("BuildAction").Value
            $newrelicDirItem.Properties.Item("BuildAction").Value = 2
            $newrelicDirItem.Properties.Item("CopyToOutputDirectory").Value = 2
        }
        catch [System.ArgumentException]{
            #Swallow - non-app project
        }
    }

    foreach ($extensionsDirItem in $extensionsDirItems) {
        try{
            $null = $extensionsDirItem.Properties.Item("BuildAction").Value
            $extensionsDirItem.Properties.Item("BuildAction").Value = 2
            $extensionsDirItem.Properties.Item("CopyToOutputDirectory").Value = 2
        }
        catch [System.ArgumentException]{
            #Swallow - non-app project
        }
    }
    
    foreach ($logsDirItem in $logsDirItems) {
        try{
            $null = $logsDirItem.Properties.Item("BuildAction").Value
            $logsDirItem.Properties.Item("BuildAction").Value = 2
            $logsDirItem.Properties.Item("CopyToOutputDirectory").Value = 2
        }
        catch [System.ArgumentException]{
            #Swallow - non-app project
        }
    }
}
