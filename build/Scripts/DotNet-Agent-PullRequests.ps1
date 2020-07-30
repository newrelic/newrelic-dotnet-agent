# Copyright 2020 New Relic Corporation. All rights reserved.
# SPDX-License-Identifier: Apache-2.0

# Update the chat room
. .\agent-build-scripts\windows\common\powershell\chatRoomAPI.ps1

if ($false) {
  # as of 19Apr2016 team no longer wants to be notified when a job starts
  $chatMessage = "$env:JOB_NAME - '$env:sha1' has started. See $env:BUILD_URL for more information."
  PostMessageToChatRoom "dotnet-agent" $chatMessage "html" 0 "gray"
}

$ErrorActionPreference = "Stop"

Set-Location -Path .\agent-build-scripts
. .\windows\common\powershell\gitHub.ps1

# Commenting out the PR checklist bot since no one seems to care about it (as of 4/14/16)
# AddPRChecklist