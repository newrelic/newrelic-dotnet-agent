#!/bin/bash

set -e

cat << REPO | tee "/etc/yum.repos.d/newrelic-dotnet-agent.repo"
[newrelic-dotnet-agent-repo]
name=New Relic .NET Core packages for Enterprise Linux
baseurl=https://yum.newrelic.com/pub/newrelic/el7/\$basearch
enabled=1
gpgcheck=1
gpgkey=file:///etc/pki/rpm-gpg/RPM-GPG-KEY-NewRelic
REPO

yum install newrelic-dotnet-agent -y

rpm -q newrelic-dotnet-agent
