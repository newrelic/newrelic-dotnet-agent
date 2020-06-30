Param
(
	[String]
	$ApplicationName,

	[String]
	$PublishProfileFile,

	[Switch]
	$UseExistingClusterConnection
	
)

function Read-XmlElementAsHashtable
{
	Param (
		[System.Xml.XmlElement]
		$Element
	)

	$hashtable = @{}
	if ($Element.Attributes)
	{
		$Element.Attributes | 
			ForEach-Object {
				$boolVal = $null
				if ([bool]::TryParse($_.Value, [ref]$boolVal)) {
					$hashtable[$_.Name] = $boolVal
				}
				else {
					$hashtable[$_.Name] = $_.Value
				}
			}
	}

	return $hashtable
}

function Read-PublishProfile
{
	Param (
		[ValidateScript({Test-Path $_ -PathType Leaf})]
		[String]
		$PublishProfileFile
	)

	$publishProfileXml = [Xml] (Get-Content $PublishProfileFile)
	$publishProfile = @{}

	$publishProfile.ClusterConnectionParameters = Read-XmlElementAsHashtable $publishProfileXml.PublishProfile.Item("ClusterConnectionParameters")
	$publishProfile.UpgradeDeployment = Read-XmlElementAsHashtable $publishProfileXml.PublishProfile.Item("UpgradeDeployment")
	$publishProfile.CopyPackageParameters = Read-XmlElementAsHashtable $publishProfileXml.PublishProfile.Item("CopyPackageParameters")

	if ($publishProfileXml.PublishProfile.Item("UpgradeDeployment"))
	{
		$publishProfile.UpgradeDeployment.Parameters = Read-XmlElementAsHashtable $publishProfileXml.PublishProfile.Item("UpgradeDeployment").Item("Parameters")
		if ($publishProfile.UpgradeDeployment["Mode"])
		{
			$publishProfile.UpgradeDeployment.Parameters[$publishProfile.UpgradeDeployment["Mode"]] = $true
		}
	}

	$publishProfileFolder = (Split-Path $PublishProfileFile)
	$publishProfile.ApplicationParameterFile = [System.IO.Path]::Combine($PublishProfileFolder, $publishProfileXml.PublishProfile.ApplicationParameterFile.Path)

	return $publishProfile
}

$LocalFolder = (Split-Path $MyInvocation.MyCommand.Path)

if (!$PublishProfileFile)
{
	$PublishProfileFile = "$LocalFolder\..\PublishProfiles\Local.xml"
}

$publishProfile = Read-PublishProfile $PublishProfileFile

if (-not $UseExistingClusterConnection)
{
	$ClusterConnectionParameters = $publishProfile.ClusterConnectionParameters
	if ($SecurityToken)
	{
		$ClusterConnectionParameters["SecurityToken"] = $SecurityToken
	}

	try
	{
		[void](Connect-ServiceFabricCluster @ClusterConnectionParameters)
		$global:clusterConnection = $clusterConnection
	}
	catch [System.Fabric.FabricObjectClosedException]
	{
		Write-Warning "Service Fabric cluster may not be connected."
		throw
	}
}

$RegKey = "HKLM:\SOFTWARE\Microsoft\Service Fabric SDK"
$ModuleFolderPath = (Get-ItemProperty -Path $RegKey -Name FabricSDKPSModulePath).FabricSDKPSModulePath
Import-Module "$ModuleFolderPath\ServiceFabricSDK.psm1"

# Remove an application instance
Unpublish-ServiceFabricApplication -ApplicationName $ApplicationName
