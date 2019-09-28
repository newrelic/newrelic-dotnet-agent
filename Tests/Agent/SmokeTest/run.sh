#!/bin/bash

# constants
package_name='newrelic-netcore20-agent'
agent_package_dir='/agent'

local_deb_package=$(find ${agent_package_dir} -name "${package_name}*.deb" |tail -n 1)
if [ -e "$local_deb_package" ]; then
    echo "Found local agent package $local_deb_package, installing it"
    dpkg -i ${agent_package_dir}/${package_name}*.deb
else
    echo "Did not find local agent package, installing from apt"
    repo_url='http://apt.newrelic.com/debian'
    repo_name='newrelic'
    echo "deb ${repo_url} ${repo_name} non-free" | tee /etc/apt/sources.list.d/newrelic.list
    wget -O- https://download.newrelic.com/548C16BF.gpg | apt-key add -
    apt-get update
    apt-get install -y ${package_name}
fi

dotnet "/app/bin/Release/netcoreapp2.1/debian-x64/AgentSmokeTest.dll" | tee -a /logs/appOutput.log