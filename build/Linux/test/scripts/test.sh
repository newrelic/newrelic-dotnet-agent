#!/bin/bash

dos2unix /test/util.sh &>/dev/null && source /test/util.sh

print_header "Simple Package Install Test"

install_agent

verify_no_logs

dotnet run

verify_logs_exist