# Copyright 2020 New Relic Corporation. All rights reserved.
# SPDX-License-Identifier: Apache-2.0

### copy core agent files to S3 ###
$parentDir = "core_20"
$betaPath = "beta"
$fullPath = "$($parentDir)\$($betaPath)"

# check if release folder exists and if not create it
New-Item -Force -ItemType directory -Path $fullPath

# clear the folder of previous run
Remove-Item "$($fullPath)\*" -recurse

# move agent and license files into the $fullPath folder
Move-Item *.* "$($fullPath)\" -force

cd $fullPath

#upload to AWS S3
aws s3 sync . "s3://nr-downloads-main/dot_net_agent/$($parentDir)/$($betaPath)" --include "*" --exclude ".DS_Store" --delete