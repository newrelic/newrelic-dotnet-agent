#!/bin/bash

dos2unix /test/util.sh &>/dev/null && source /test/util.sh

print_header "HTTP Client Test"

install_tarball "/custom/install/path"

verify_no_logs

$CORECLR_NEW_RELIC_HOME/run.sh dotnet run

verify_logs_exist

verify_agent_log_exists "http_client_test"