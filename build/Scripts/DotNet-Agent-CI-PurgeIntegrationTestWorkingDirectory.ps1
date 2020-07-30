# Copyright 2020 New Relic Corporation. All rights reserved.
# SPDX-License-Identifier: Apache-2.0

# Delete the contents of 'C:\IntegrationTestWorkingDirectory'
Write-Host "Purging the contents of 'C:\IntegrationTestWorkingDirectory'"
Remove-Item -Path C:\IntegrationTestWorkingDirectory\* -Recurse -Force -ErrorAction SilentlyContinue
