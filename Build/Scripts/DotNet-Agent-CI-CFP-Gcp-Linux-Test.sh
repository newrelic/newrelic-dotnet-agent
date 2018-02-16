#!/bin/bash

serverIP="35.199.160.54"
testDirectoryName="utilization_tests"
installerName=$(ls -a *.rpm)
testAppPath="~/core_apps/DotNet-Integration-CFP-Core20-App/bin/Debug/netcoreapp2.0/DotNet-Integration-CFP-Core20-App.dll"
CORECLR_NEWRELIC_HOME="/usr/local/newrelic-netcore20-agent"

function CreateTestDirectory() {
	echo "Creating Test Directory"
	ssh -i ~/.ssh/utilization_rsa jenkins@$serverIP "mkdir ~/$testDirectoryName"
}

function CopyInstaller() {
	echo "Copying Installer"
	scp -i ~/.ssh/utilization_rsa ./$installerName jenkins@$serverIP:./$testDirectoryName/$installerName
}

function RemoveTestDirectory() {
	echo "Removing Test Directory"
	ssh -i ~/.ssh/utilization_rsa jenkins@$serverIP "rm -r ~/$testDirectoryName"
}

function InstallAgent() {
	echo "Installing Agent"
	ssh -i ~/.ssh/utilization_rsa jenkins@$serverIP "sudo yum install ~/utilization_tests/$installerName -y"
}

function RemoveAgent() {
	echo "Removing Agent"
	ssh -i ~/.ssh/utilization_rsa jenkins@$serverIP "sudo yum remove newrelic-netcore20-agent -y"
}

function SpinUpApp() {
	echo "Spinning Up App"
	ssh -i ~/.ssh/utilization_rsa jenkins@$serverIP "nohup $CORECLR_NEWRELIC_HOME/run.sh dotnet $testAppPath > ~/utilization_tests/output.log 2>&1 &"
	ssh -i ~/.ssh/utilization_rsa jenkins@$serverIP 'ps -e | grep dotnet >> ~/utilization_tests/pidfile.txt'
	sleep 5 
}

function GetPid() {
	scp -i ~/.ssh/utilization_rsa jenkins@$serverIP:~/utilization_tests/pidfile.txt ./
	pid=$(awk '{print $1}' pidfile.txt)
 	echo $pid	
}

function KillApp() {
	echo "Killing App"
	pid=$1
	ssh -i ~/.ssh/utilization_rsa jenkins@$serverIP "sudo kill -9 $pid"
}

function ExerciseApp() {
	echo "Exercising App at $serverIP"
	curl "http://$serverIP:8888"
	sleep 5
}

function GetUtilizationString() {
	curl -s "http://$serverIP:8888/Logs/UtilizationString"
}


function ValidateUtilizationString() {
	id=$(echo "$1" | jq '.[].utilization.vendors.gcp.id')
	machineType=$(echo "$1" | jq '.[].utilization.vendors.gcp.machineType')
	name=$(echo "$1" | jq '.[].utilization.vendors.gcp.name')
	zone=$(echo "$1" | jq '.[].utilization.vendors.gcp.zone')
	
	if [ "$id" != "null" ] && [ "$machineType" != "null" ] && [ "$name"  != "null" ] && [ "$zone" != "null" ] 
	then
		echo valid
	else
		echo invalid
	fi 
}

function DiscardLogs() {
	echo "Discarding Logs"
	ssh -i ~/.ssh/utilization_rsa jenkins@$serverIP "sudo rm -r /var/log/newrelic/dotnet/*.*"
}

# Setup
CreateTestDirectory
CopyInstaller
InstallAgent
SpinUpApp
pid=$(GetPid)

# Test
ExerciseApp
echo "Getting Utilization String"
utilizationString=$(GetUtilizationString)
echo "Validating Utiliazation String"
valid=$(ValidateUtilizationString "$utilizationString")
echo "Data is: $valid"

# Cleanup
KillApp $pid
RemoveAgent
DiscardLogs
RemoveTestDirectory

if [ "$valid" == "valid" ]
then
	echo "Test Result: Success"
	exit 0
else
	echo "Test Result: Failure"
	exit 1
fi

