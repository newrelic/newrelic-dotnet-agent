#!/bin/bash

# Starts a loop to check if SQL is ready before running setup script.
STATUS=$(/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P $MSSQL_SA_PASSWORD -No -d master -Q "SET NOCOUNT ON; SELECT 1" -W -h-1 )
while [ "$STATUS" != 1 ]
do
sleep 1s
STATUS=$(/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P $MSSQL_SA_PASSWORD -No -d master -Q "SET NOCOUNT ON; SELECT 1" -W -h-1 )
done

/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P $MSSQL_SA_PASSWORD -No -i /var/restore.sql