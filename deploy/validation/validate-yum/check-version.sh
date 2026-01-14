#!/bin/bash

set -e

yum install newrelic-dotnet-agent -y

rpm -q --queryformat '%{VERSION}\n' newrelic-dotnet-agent
