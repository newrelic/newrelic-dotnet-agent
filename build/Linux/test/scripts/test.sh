#!/bin/bash

dos2unix /test/util.sh &>/dev/null && source /test/util.sh

print_header "Simple Package Install Test"

install_agent

verify_no_logs

# run the sample mvc app in the background and make a request to it to generate logs
dotnet ./publish/data.dll --urls "http://+:8080" >/dev/null 2>&1 &
sleep 5
curl http://localhost:8080 >/dev/null 2>&1

verify_logs_exist

kill %1
