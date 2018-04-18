
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

# Prompt use to enter a name then >> Solution name >> more than one role we will attempt to use worker role name
function set_newrelic_appname_config_node([System.Xml.XmlElement]$node, [System.String]$pn){
	$appName = create_dialog "NewRelic.AppName Key" "Please enter the value you would like for the NewRelic.AppName AppSetting for the project named $pn (optional, if none is provided we will use the solution name)"
	if($node -ne $null){
		if($appname -ne $null -and $appName.Length -gt 0){
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
	$config = $project.ProjectItems.Item("Web.Config")
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
			$configXml.configuration["appSettings"].appendchild($addSettingNode)
		}
		
		$configXml.Save($configPath);
	}
}