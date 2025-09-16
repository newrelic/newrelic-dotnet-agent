#!/bin/bash

set -e

echo 'deb [signed-by=/usr/share/keyrings/newrelic-apt.gpg] http://apt.newrelic.com/debian/ newrelic non-free' | tee /etc/apt/sources.list.d/newrelic.list
apt-get update && apt-get install newrelic-dotnet-agent -y

dpkg -s newrelic-dotnet-agent | grep '^Version:' | cut -d' ' -f2
