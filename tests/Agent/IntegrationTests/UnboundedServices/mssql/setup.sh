#!/bin/bash

# Starts a loop to check if SQL is ready before running setup script.
STATUS=$(/opt/mssql-tools/bin/sqlcmd -S localhost -U sa -d master -Q "SET NOCOUNT ON; SELECT 1" -W -h-1 )
while [ "$STATUS" != 1 ]
do
sleep 1s
STATUS=$(/opt/mssql-tools/bin/sqlcmd -S localhost -U sa -d master -Q "SET NOCOUNT ON; SELECT 1" -W -h-1 )
done

/opt/mssql-tools/bin/sqlcmd -S localhost -U sa -i /var/restore.sql