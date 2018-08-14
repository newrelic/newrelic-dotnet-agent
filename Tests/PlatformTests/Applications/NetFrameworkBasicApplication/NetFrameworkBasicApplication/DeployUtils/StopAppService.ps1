Param(
	[string] $appName,
	[string] $publishSettings
)

Import-AzurePublishSettingsFile -PublishSettingsFile $publishSettings
Select-AzureSubscription -SubscriptionName '.Net Team Sandbox'
Stop-AzureWebsite -Name $appName
