############################################################
# Copyright 2020 New Relic Corporation. All rights reserved.
# SPDX-License-Identifier: Apache-2.0
############################################################

#!/bin/sh

CORECLR_NEWRELIC_HOME=./linux/test/agent docker-compose run test bash -c "cd /test/custom_attributes && CORECLR_ENABLE_PROFILING=0 dotnet build && CORECLR_NEWRELIC_INSTALL_PATH=/test/custom_attributes/bin/Debug/netcoreapp2.0/custom_attributes.dll dotnet vstest bin/Debug/netcoreapp2.0/custom_attributes.dll"

CORECLR_NEWRELIC_HOME=./linux/test/agent docker-compose run test_centos bash -c "cd /test/custom_attributes && CORECLR_ENABLE_PROFILING=0 dotnet build && CORECLR_NEWRELIC_INSTALL_PATH=/test/custom_attributes/bin/Debug/netcoreapp2.0/custom_attributes.dll dotnet vstest bin/Debug/netcoreapp2.0/custom_attributes.dll"
