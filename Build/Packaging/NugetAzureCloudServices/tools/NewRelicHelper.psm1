
function create_dialog([System.String]$title, [System.String]$msg){
	[void] [System.Reflection.Assembly]::LoadWithPartialName("System.Drawing") 
	[void] [System.Reflection.Assembly]::LoadWithPartialName("System.Windows.Forms") 

	$objForm = New-Object System.Windows.Forms.Form 
	$objForm.Text = $title
	$objForm.Size = New-Object System.Drawing.Size(300,200) 
	$objForm.StartPosition = "CenterScreen"
	$objForm.FormBorderStyle = "FixedDialog"

	$objForm.KeyPreview = $True
	$objForm.Add_KeyDown({if ($_.KeyCode -eq "Enter") 
		{$script:x=$objTextBox.Text;$objForm.Close()}})
	$objForm.Add_KeyDown({if ($_.KeyCode -eq "Escape") 
		{$script:x=$null;$objForm.Close()}})

	$OKButton = New-Object System.Windows.Forms.Button
	$OKButton.Location = New-Object System.Drawing.Size(75,120)
	$OKButton.Size = New-Object System.Drawing.Size(75,23)
	$OKButton.Text = "OK"
	$OKButton.Add_Click({$script:x=$objTextBox.Text;$objForm.Close()})
	$objForm.Controls.Add($OKButton)

	$CancelButton = New-Object System.Windows.Forms.Button
	$CancelButton.Location = New-Object System.Drawing.Size(150,120)
	$CancelButton.Size = New-Object System.Drawing.Size(75,23)
	$CancelButton.Text = "Cancel"
	$CancelButton.Add_Click({$script:x=$null;$objForm.Close()})
	$objForm.Controls.Add($CancelButton)

	$objLabel = New-Object System.Windows.Forms.Label
	$objLabel.Location = New-Object System.Drawing.Size(10,20) 
	$objLabel.Size = New-Object System.Drawing.Size(280,60) 
	$objLabel.Text = $msg
	$objForm.Controls.Add($objLabel) 

	$objTextBox = New-Object System.Windows.Forms.TextBox 
	$objTextBox.Location = New-Object System.Drawing.Size(10,80) 
	$objTextBox.Size = New-Object System.Drawing.Size(260,20) 
	$objForm.Controls.Add($objTextBox) 

	$objForm.Topmost = $True

	$objForm.Add_Shown({$objForm.Activate()})
	[void] $objForm.ShowDialog()
	return $x
}

#Modify NewRelic.msi and NewRelic.cmd so that they will be copy always
function update_newrelic_project_items([System.__ComObject] $project, [System.String]$agentMsi, [System.String]$serverMonitorMsi){
	$newrelicAgentMsi = $project.ProjectItems.Item($agentMsi)
	$copyToOutputMsi = $newrelicAgentMsi.Properties.Item("CopyToOutputDirectory")
	$copyToOutputMsi.Value = 1

	$newrelicServerMonitorMsi = $project.ProjectItems.Item($serverMonitorMsi)
	$copyToOutputMsi = $newrelicServerMonitorMsi.Properties.Item("CopyToOutputDirectory")
	$copyToOutputMsi.Value = 1

	$newrelicCmd = $project.ProjectItems.Item("newrelic.cmd")
	$copyToOutputCmd = $newrelicCmd.Properties.Item("CopyToOutputDirectory")
	$copyToOutputCmd.Value = 1
}

#Modify all ServiceConfiguration.*.cscfg to add New Relice license key
function update_azure_service_configs([System.__ComObject] $project){
	
	$pn = $project.Name.ToString()
    $licenseKey = $null;
	
	$svcConfigFiles = $DTE.Solution.Projects | Select-Object -Expand ProjectItems | Where-Object{$_.Name -like 'ServiceConfiguration.*.cscfg'}
	
	if($svcConfigFiles -eq $null){
		Write-Host "Unable to find any ServiceConfiguration.cscfg files in your solution, please make sure your solution contains an Azure deployment project and try again."
		return
	}

	foreach ($svcConfigFile in $svcConfigFiles) {
		$ServiceConfig = $svcConfigFile.Properties.Item("FullPath").Value
		if(!(Test-Path $ServiceConfig)) {
			Write-Host "Unable to locate the ServiceConfiguration.cscfg file on your filesystem.  Please verify that the ServiceConfiguration.cscfg in your solution points to a valid file and try again."
			return
		}

		[xml] $xml = gc $ServiceConfig

		$configSettingsNode = $xml.CreateElement('ConfigurationSettings','http://schemas.microsoft.com/ServiceHosting/2008/10/ServiceConfiguration')
		$settingNode = $xml.CreateElement('Setting','http://schemas.microsoft.com/ServiceHosting/2008/10/ServiceConfiguration')

		$settingNode.SetAttribute('name', 'NewRelic.LicenseKey')
		$configSettingsNode.AppendChild($settingNode)

		foreach($i in $xml.ServiceConfiguration.ChildNodes){
			if($i.name -eq $pn){
                $modified = $i
				break
			}
		}
		
		#Make sure a matching app was found
        if($modified -ne $null){
        
            $ns = new-object Xml.XmlNamespaceManager $xml.NameTable
			$ns.AddNamespace('dns', 'http://schemas.microsoft.com/ServiceHosting/2008/10/ServiceConfiguration')
			$modifiedConfigSettings = $modified.SelectSingleNode("/dns:ServiceConfiguration/dns:Role[@name='$pn']/dns:ConfigurationSettings", $ns) 

    		if ($modifiedConfigSettings -eq $null){
    			# Moved dialog here because if the value already exists, no need to ask again
    			if ($licenseKey -eq $null) {
    				$licenseKey = create_dialog "License Key" "Please enter your New Relic LICENSE KEY"
    			}
    
    			$settingNode.SetAttribute('value', $licenseKey)
    			$modified.AppendChild($configSettingsNode)
    		}
    		else{
    			$nodeExists = $false
    			foreach ($i in $modifiedConfigSettings.Setting){
    				if ($i.name -eq "NewRelic.LicenseKey"){
    					$nodeExists = $true
    				}
    			}
    			if($NewRelicTask -eq $null -and !$nodeExists){
    				# Moved dialog here because if the value already exists, no need to ask again
    				if ($licenseKey -eq $null) {
    					$licenseKey = create_dialog "License Key" "Please enter your New Relic LICENSE KEY"
    				}
    
    				$settingNode.SetAttribute('value', $licenseKey)
    				$modifiedConfigSettings.AppendChild($settingNode)
    			}
    		}
    		
    		$xml.Save($ServiceConfig);
		}
	}
}

#Modify the service config - adding a new Startup task to run the newrelic.cmd
function update_azure_service_definition([System.__ComObject] $project){
	
	$pn = $project.Name.ToString()
	
	$svcConfigFiles = $DTE.Solution.Projects | Select-Object -Expand ProjectItems | Where-Object{$_.Name -eq 'ServiceDefinition.csdef'}
	
	if($svcConfigFiles -eq $null){
		Write-Host "Unable to find the ServiceDefinition.csdef file in your solution, please make sure your solution contains an Azure deployment project and try again."
		return
	}
	
	foreach ($svcConfigFile in $svcConfigFiles) {
		$ServiceDefinitionConfig = $svcConfigFile.Properties.Item("FullPath").Value
		if(!(Test-Path $ServiceDefinitionConfig)) {
			Write-Host "Unable to locate the ServiceDefinition.csdef file on your filesystem.  Please verify that the ServiceDefinition.csdef in your solution points to a valid file and try again."
			return
		}
		
		[xml] $xml = gc $ServiceDefinitionConfig

		$isWorkerRole = 'false'
        $role = "WebRole"
        
        #Create startup and newrelic task nodes
        $startupNode = $xml.CreateElement('Startup','http://schemas.microsoft.com/ServiceHosting/2008/10/ServiceDefinition')
        $taskNode = $xml.CreateElement('Task','http://schemas.microsoft.com/ServiceHosting/2008/10/ServiceDefinition')
        $environmentNode = $xml.CreateElement('Environment','http://schemas.microsoft.com/ServiceHosting/2008/10/ServiceDefinition')
        $variableNode = $xml.CreateElement('Variable','http://schemas.microsoft.com/ServiceHosting/2008/10/ServiceDefinition')
        $roleInstanceValueNode = $xml.CreateElement('RoleInstanceValue','http://schemas.microsoft.com/ServiceHosting/2008/10/ServiceDefinition')
        
        $roleInstanceValueNode.SetAttribute('xpath','/RoleEnvironment/Deployment/@emulated')
        $variableNode.SetAttribute('name','EMULATED')
        
        $variableNode.AppendChild($roleInstanceValueNode)
        $environmentNode.AppendChild($variableNode)
        
        $taskNode.SetAttribute('commandLine','newrelic.cmd')
        $taskNode.SetAttribute('executionContext','elevated')
        $taskNode.SetAttribute('taskType','simple')
        
        foreach($i in $xml.ServiceDefinition.ChildNodes){
        	if($i.name -eq $pn){
	            $modified = $i
        		if($modified.LocalName -eq 'WorkerRole'){
        		    $isWorkerRole = 'true'
        		    Write-Host "Azure Worker Role projects have no default concept of Web transactions or HTTP context so you'll need to use the Agent API (added as a reference to your project as part of this package) for your custom instrumentation needs."
        		    Write-Host "Please visit https://newrelic.com/docs/dotnet/the-net-agent-api for more on the NewRelic.Api.Agent"
        		}
        		break
        	}
        }
        
        #Make sure a matching app was found
        if($modified -ne $null){
            #Generate the variable for the startup task that will be used to check to see if we need to issue a restart on the W3SVC 
            $variableIsWorkerRoleNode = $xml.CreateElement('Variable','http://schemas.microsoft.com/ServiceHosting/2008/10/ServiceDefinition')
            $variableIsWorkerRoleNode.SetAttribute('name','IsWorkerRole')
            $variableIsWorkerRoleNode.SetAttribute('value',$isWorkerRole)
            $environmentNode.AppendChild($variableIsWorkerRoleNode)
            
            #Generate the LICENSE_KEY variable
            $licenseKeyVariableNode = $xml.CreateElement('Variable','http://schemas.microsoft.com/ServiceHosting/2008/10/ServiceDefinition')
            $licenseKeyRoleInstanceValueNode = $xml.CreateElement('RoleInstanceValue','http://schemas.microsoft.com/ServiceHosting/2008/10/ServiceDefinition')
            $licenseKeyRoleInstanceValueNode.SetAttribute('xpath','/RoleEnvironment/CurrentInstance/ConfigurationSettings/ConfigurationSetting[@name=''NewRelic.LicenseKey'']/@value')
            $licenseKeyVariableNode.SetAttribute('name','LICENSE_KEY')
            $licenseKeyVariableNode.AppendChild($licenseKeyRoleInstanceValueNode)
            $environmentNode.AppendChild($licenseKeyVariableNode)
            
            $taskNode.AppendChild($environmentNode)
            $startupNode.AppendChild($taskNode)
        
            $modifiedStartUp = $modified.StartUp
            if($modifiedStartUp -eq $null){
            	$modified.PrependChild($startupNode)
            }
            else{
            	$nodeExists = $false
            	foreach ($i in $modifiedStartUp.Task){
            		if ($i.commandLine -eq "newrelic.cmd"){
            			$nodeExists = $true
            		}
            	}
            	if($NewRelicTask -eq $null -and !$nodeExists){
            		$modifiedStartUp.AppendChild($taskNode)
            	}
            }
            
            if($isWorkerRole -eq 'true'){
            
                $role = "WorkerRole"    
                
            	#Generate the environment variables for worker role instrumentation
            	$runtimeNode = $xml.CreateElement('Runtime','http://schemas.microsoft.com/ServiceHosting/2008/10/ServiceDefinition')
            	$runtimeEnvironmentNode = $xml.CreateElement('Environment','http://schemas.microsoft.com/ServiceHosting/2008/10/ServiceDefinition')
            	
            	#Enables profiling for all CLR based applications
            	$variableCEPNode = $xml.CreateElement('Variable','http://schemas.microsoft.com/ServiceHosting/2008/10/ServiceDefinition')
            	$variableCEPNode.SetAttribute('name','COR_ENABLE_PROFILING')
            	$variableCEPNode.SetAttribute('value','1')
            	
            	#Profiler guid associated with the New Relic .NET profiler
            	$variableCPNode = $xml.CreateElement('Variable','http://schemas.microsoft.com/ServiceHosting/2008/10/ServiceDefinition')
            	$variableCPNode.SetAttribute('name','COR_PROFILER')
            	$variableCPNode.SetAttribute('value','{71DA0A04-7777-4EC6-9643-7D28B46A8A41}')
            	
            	#Helps Azure Workers find the newrelic.config
            	$variableNHNode = $xml.CreateElement('Variable','http://schemas.microsoft.com/ServiceHosting/2008/10/ServiceDefinition')
            	$variableNHNode.SetAttribute('name','NEWRELIC_HOME')
                $variableNHNode.SetAttribute('value','D:\ProgramData\New Relic\.NET Agent\')
                
                #Helps Azure Workers find the NewRelic.Agent.Core.dll
            	$variableNIPNode = $xml.CreateElement('Variable','http://schemas.microsoft.com/ServiceHosting/2008/10/ServiceDefinition')
            	$variableNIPNode.SetAttribute('name','NEWRELIC_INSTALL_PATH')
                $variableNIPNode.SetAttribute('value','D:\Program Files\New Relic\.NET Agent\')
                
            	$runtimeEnvironmentNode.AppendChild($variableCEPNode)
            	$runtimeEnvironmentNode.AppendChild($variableCPNode)
            	$runtimeEnvironmentNode.AppendChild($variableNHNode)
            	$runtimeEnvironmentNode.AppendChild($variableNIPNode)
            	$runtimeNode.AppendChild($runtimeEnvironmentNode)
            
            	$modifiedRuntime = $modified.Runtime
            	if($modifiedRuntime -eq $null){
            		$modified.PrependChild($runtimeNode)
            	}
            }
            
            #Add 'NewRelic.LicenseKey' to ConfigurationSettings
            $configSettingsNode = $xml.CreateElement('ConfigurationSettings','http://schemas.microsoft.com/ServiceHosting/2008/10/ServiceDefinition')
            $settingNode = $xml.CreateElement('Setting','http://schemas.microsoft.com/ServiceHosting/2008/10/ServiceDefinition')
            
            $settingNode.SetAttribute('name', 'NewRelic.LicenseKey')
            $configSettingsNode.AppendChild($settingNode)
            
            $ns = new-object Xml.XmlNamespaceManager $xml.NameTable
			$ns.AddNamespace('dns', 'http://schemas.microsoft.com/ServiceHosting/2008/10/ServiceDefinition')
			$modifiedConfigSettings = $modified.SelectSingleNode("/dns:ServiceDefinition/dns:$role[@name='$pn']/dns:ConfigurationSettings", $ns) 

            if ($modifiedConfigSettings -eq $null){
            	$modified.AppendChild($configSettingsNode)
            }
            else{
            	$nodeExists = $false
            	foreach ($i in $modifiedConfigSettings.Setting){
            		if ($i.name -eq "NewRelic.LicenseKey"){
            			$nodeExists = $true
            		}
            	}
            	if($NewRelicTask -eq $null -and !$nodeExists){
            		$modifiedConfigSettings.AppendChild($settingNode)
            	}
            }
            
    		$xml.Save($ServiceDefinitionConfig);
		}
	}
}

# Depending on how many worker roles / web roles there are in this project 
# we will use this value for the config key NewRelic.AppName
# Prompt use to enter a name then >> Solution name >> more than one role we will attempt to use worker role name
function set_newrelic_appname_config_node([System.Xml.XmlElement]$node, [System.String]$pn){
	$appName = create_dialog "NewRelic.AppName" "Please enter the value you would like for the NewRelic.AppName AppSetting for the project named $pn (optional, if none is provided we will use the solution name)"
	if($node -ne $null){
		if($appName -ne $null -and $appName.Length -gt 0){
			$node.SetAttribute('value',$appName)
		}
		else{
			if($node.value.Length -lt 1){
				$node.SetAttribute('value',$pn)
			}
		}
	}
	return $node
}

#Modify the [web|app].config so that we can use the project name instead of a static placeholder
function update_project_config([System.__ComObject] $project){
	Try{
		$config = $project.ProjectItems.Item("Web.Config")
	}Catch{
		#Swallow - non webrole project 
	}
	if($config -eq $null){
		$config = $project.ProjectItems.Item("App.Config")
	}
	
	$configPath = $config.Properties.Item("LocalPath").Value
	[xml] $configXml = gc $configPath

	if($configXml -ne $null){
		$newRelicAppSetting = $null
		if(!$configXml.configuration.appSettings.IsEmpty -and $configXml.configuration.appSettings.HasChildNodes){
			$newRelicAppSetting = $configXml.configuration.appSettings.SelectSingleNode("//add[@key = 'NewRelic.AppName']")
		}

		if($newRelicAppSetting -ne $null){
			set_newrelic_appname_config_node $newRelicAppSetting $project.Name.ToString()
		}
		else{
			#add the node
			$addSettingNode = $configXml.CreateElement('add')
			$addSettingNode.SetAttribute('key','NewRelic.AppName')
			set_newrelic_appname_config_node $addSettingNode $project.Name.ToString()
			
			if($configXml.configuration.appSettings -eq $null){
				$addAppSettingsNode = $configXml.CreateElement('appSettings')
				$configXml.configuration.appendchild($addAppSettingsNode)
			}
			
			$configXml.configuration["appSettings"].appendchild($addSettingNode)
		}
		
		$configXml.Save($configPath);
	}
}

#Modify the service defintion - removing the Startup task to run the newrelic.cmd
function cleanup_azure_service_definition([System.__ComObject] $project){
	$svcConfigFiles = $DTE.Solution.Projects|Select-Object -Expand ProjectItems|Where-Object{$_.Name -eq 'ServiceDefinition.csdef'}
	if($svcConfigFiles -eq $null){
		return
	}
	
	foreach ($svcConfigFile in $svcConfigFiles) {
		$ServiceDefinitionConfig = $svcConfigFile.Properties.Item("FullPath").Value
		if(!(Test-Path $ServiceDefinitionConfig)) {
			return
		}
		
		[xml] $xml = gc $ServiceDefinitionConfig

		foreach($i in $xml.ServiceDefinition.ChildNodes){
			if($i.name -eq $project.Name.ToString()){
				$modified = $i
				break
			}
		}

		$startupnode = $modified.Startup
		if($startupnode.ChildNodes.Count -gt 0){
			$node = $startupnode.Task | where { $_.commandLine -eq "newrelic.cmd" }
			if($node -ne $null){
				[Void]$node.ParentNode.RemoveChild($node)
				if($startupnode.ChildNodes.Count -eq 0){
					[Void]$startupnode.ParentNode.RemoveChild($startupnode)
				}
				$xml.Save($ServiceDefinitionConfig)
			}
		}
		
		$runtimeNode = $modified.Runtime
        if($runtimeNode -ne $null -and $runtimeNode.ChildNodes.Count -gt 0){
        	$variableNodes = $runtimeNode.Environment.Variable | where { $_.name -eq "COR_ENABLE_PROFILING" -or $_.name -eq "COR_PROFILER" -or $_.name -eq "NEWRELIC_HOME" -or $_.name -eq "NEWRELIC_INSTALL_PATH" }
        	if($variableNodes -ne $null -and $variableNodes.Count -gt 0){
        		foreach($varNode in $variableNodes){
        			[Void]$varNode.ParentNode.RemoveChild($varNode)
        		}
        		if($runtimeNode.Environment.ChildNodes.Count -eq 0){
        			[Void]$runtimeNode.ParentNode.RemoveChild($runtimeNode)
        		}
        		$xml.Save($ServiceDefinitionConfig)
        	}
        }
        
        # Remove NewRelic.LicenseKey configuration setting from service definition
        $configSettingsNode = $modified.ConfigurationSettings
        if($configSettingsNode -ne $null -and $configSettingsNode.ChildNodes.Count -gt 0){
        	$node = $configSettingsNode.Setting | where { $_.name -eq "NewRelic.LicenseKey" }
        	if($node -ne $null){
        		[Void]$node.ParentNode.RemoveChild($node)
        		if($configSettingsNode.ChildNodes.Count -eq 0){
        			[Void]$configSettingsNode.ParentNode.RemoveChild($configSettingsNode)
        		}
        		$xml.Save($ServiceDefinitionConfig)
        	}
        }
    }
}

#Modify the service configs - removing the NewRelic.LicenseKey config setting
function cleanup_azure_service_configs([System.__ComObject] $project){
	$svcConfigFiles = $DTE.Solution.Projects | Select-Object -Expand ProjectItems | Where-Object{$_.Name -like 'ServiceConfiguration.*.cscfg'}
	
	if($svcConfigFiles -eq $null){
		return
	}

	foreach ($svcConfigFile in $svcConfigFiles) {
		$ServiceConfig = $svcConfigFile.Properties.Item("FullPath").Value
		if(!(Test-Path $ServiceConfig)) {
			return
		}

		[xml] $xml = gc $ServiceConfig

		foreach($i in $xml.ServiceConfiguration.ChildNodes){
			if($i.name -eq $project.Name.ToString()){
				$modified = $i
				break
			}
		}

		$configSettingsNode = $modified.ConfigurationSettings
		if($configSettingsNode.ChildNodes.Count -gt 0){
			$node = $configSettingsNode.Setting | where { $_.name -eq "NewRelic.LicenseKey" }
			if($node -ne $null){
				[Void]$node.ParentNode.RemoveChild($node)
				if($configSettingsNode.ChildNodes.Count -eq 0){
					[Void]$configSettingsNode.ParentNode.RemoveChild($configSettingsNode)
				}
				$xml.Save($ServiceConfig)
			}
		}
	}
}

#Remove all newrelic info from the [web|app].config
function cleanup_project_config([System.__ComObject] $project){
	Try{
		$config = $project.ProjectItems.Item("Web.Config")
	}Catch{
		#Swallow - non webrole project 
	}
	if($config -eq $null){
		$config = $DTE.Solution.FindProjectItem("App.Config")
	}
	$configPath = $config.Properties.Item("LocalPath").Value
	if((Test-Path $configPath)) {
		[xml] $configXml = gc $configPath

		if($configXml -ne $null){	
			$newRelicAppSetting = $configXml.configuration.appSettings.SelectSingleNode("//add[@key = 'NewRelic.AppName']")
			if($newRelicAppSetting -ne $null){
				[Void]$newRelicAppSetting.ParentNode.RemoveChild($newRelicAppSetting)
				$configXml.Save($configPath)
			}
		}
	}
	
	# manually remove newrelic.cmd since the Nuget uninstaller won't due to it being "modified"
	Try{
		$scripts = $project.ProjectItems | Where-Object { $_.Name -eq "newrelic.cmd" }

		if ($scripts) {
			$scripts.ProjectItems | ForEach-Object { $_.Delete() }
		}
	}Catch{
		#Swallow - file has been removed
	}
}
