############################################################
# Copyright 2020 New Relic Corporation. All rights reserved.
# SPDX-License-Identifier: Apache-2.0
############################################################

Param(
	[string] $appName,
	[string] $publishSettings
)

Import-AzurePublishSettingsFile -PublishSettingsFile $publishSettings
Select-AzureSubscription -SubscriptionName '.Net Team Sandbox'
Start-AzureWebsite -Name $appName
