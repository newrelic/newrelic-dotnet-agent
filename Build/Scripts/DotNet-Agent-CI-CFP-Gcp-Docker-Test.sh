#!/bin/bash
serverIP="35.199.160.54"
appPort="8888"
testDirectoryName="utilization_tests"
installerName=$(ls -a *64*.deb)
appName="DotNet-Integration-CFP-Core20-App"
dockerImageTag="cfp-test"

function CreateTestDirectory() { 	
	echo "Creating Test Directory"
	ssh -i ~/.ssh/utilization_rsa jenkins@$serverIP "mkdir ~/$testDirectoryName"
}

function RemoveTestDirectory() {
	echo "Removing Test Directory"
	ssh -i ~/.ssh/utilization_rsa jenkins@$serverIP "rm -r ~/$testDirectoryName"
}

function CreateDockerFile() {
	
	echo "Creating DockerFile"

read -d '' dockerFileString <<"EOF"
FROM microsoft/dotnet:2.0.0-sdk-2.0.2-stretch

ENV CORECLR_ENABLE_PROFILING="1" \
CORECLR_PROFILER="{36032161-FFC0-4B61-B559-F6C5D41BAE5A}" \
CORECLR_NEWRELIC_HOME="/usr/local/newrelic-netcore20-agent" \
CORECLR_PROFILER_PATH="/usr/local/newrelic-netcore20-agent/libNewRelicProfiler.so" \
NEW_RELIC_LICENSE_KEY="b25fd3ca20fe323a9a7c4a092e48d62dc64cc61d" \
NEW_RELIC_APP_NAME="DotNet-Integration-CFP-Core20-App"

WORKDIR /app

ARG runtimeIdentifier=debian-x64
ARG files=./appName/bin/Debug/netcoreapp2.0/$runtimeIdentifier/publish
COPY $files ./appcode

ARG NewRelic=./newrelic
COPY $NewRelic ./newrelic

RUN dpkg -i ./newrelic/installerName

ENV ASPNETCORE_URLS http://+:appPort
EXPOSE appPort

WORKDIR /app/appcode
ENTRYPOINT ["dotnet", "./appName.dll"]
EOF
	dockerFileString="${dockerFileString/appPort/$appPort}"
	dockerFileString="${dockerFileString/appPort/$appPort}"
	dockerFileString="${dockerFileString/appName/$appName}"
	dockerFileString="${dockerFileString/appName/$appName}"
	dockerFileString="${dockerFileString/installerName/$installerName}"
	dockerFileString="${dockerFileString/installerName/$installerName}"
	echo "${dockerFileString}" >> DockerFile
}

function CopyTestFiles() {
	echo "Copy Test Files"
	ssh -i ~/.ssh/utilization_rsa jenkins@$serverIP "cp -r ~/core_apps/$appName ~/$testDirectoryName/"
	scp -i ~/.ssh/utilization_rsa ./* jenkins@$serverIP:~/$testDirectoryName/$appname/
	ssh -i ~/.ssh/utilization_rsa jenkins@$serverIP "mkdir ~/$testDirectoryName/newrelic ; mv ~/$testDirectoryName/*.deb ~/$testDirectoryName/newrelic/"
}

function CreateDockerImage() {
	echo "Creating Docker Image"
	ssh -i ~/.ssh/utilization_rsa jenkins@$serverIP "cd $testDirectoryName ; docker build --rm -t $dockerImageTag -f ./DockerFile ./"
}


function RunDockerImage() {
	echo "Run Docker Image"
	ssh -i ~/.ssh/utilization_rsa jenkins@$serverIP "nohup docker run --rm --name $dockerImageTag --network=host $dockerImageTag >> /dev/null &" 
	sleep 5 
}

function KillDockerContainer() {
	echo "Killing Docker Container"
	ssh -i ~/.ssh/utilization_rsa jenkins@$serverIP "docker kill $dockerImageTag"
}

function RemoveDockerImage() {
	echo "Removing Docker Image"
	ssh -i ~/.ssh/utilization_rsa jenkins@$serverIP "docker rmi $dockerImageTag"
}

function RemoveDockerFile() {
	echo "Removing DockerFile"
	rm DockerFile
}

function ValidateUtilizationString() {
	utilizationString=$(curl http://$serverIP:$appPort/logs/utilizationString)
	id=$(echo "$utilizationString" | jq '.[].utilization.vendors.docker.id')

	if [ "$id" != "null" ]
	then
		echo valid
	else
		echo invalid
	fi
}

# Setup
CreateDockerFile
CreateTestDirectory
CopyTestFiles
CreateDockerImage
RunDockerImage

# Exercise and Validate
echo "Validating Utiliazation String"
valid=$(ValidateUtilizationString)

# Cleanup
KillDockerContainer
RemoveDockerImage
RemoveDockerFile
RemoveTestDirectory

if [ "$valid" == "valid" ]
then
	echo "Test Result: Success"
	exit 0
else
	echo "Test Result: Failure"
	exit 1
fi
