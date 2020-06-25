############################################################
# Copyright 2020 New Relic Corporation. All rights reserved.
# SPDX-License-Identifier: Apache-2.0
############################################################

function SetProjectItemProperties($root) {
	foreach ($item in $root) {
		foreach ($subItem in $item.ProjectItems) {
			SetProjectItemProperties($subItem)
		}

		# GUID_ItemType_PhysicalFile - https://docs.microsoft.com/en-us/visualstudio/extensibility/ide-guids
		if ($item.Kind -eq "{6BB5F8EE-4483-11D3-8BCF-00C04F8EC28C}") {
			$item.Properties.Item("BuildAction").Value = 0 # None
			$item.Properties.Item("CopyToOutputDirectory").Value = 2 # Copy if newer
		}
	}
}

function Set-NewRelicProjectItemProperties($project) {
	SetProjectItemProperties($project.ProjectItems.Item("newrelic"))
}

Export-ModuleMember Set-NewRelicProjectItemProperties