#!/bin/bash

# start SQL Server - start the script to restore the DB
# SQL startup MUST be at the end of this line to keep container up
 /var/setup.sh & /opt/mssql/bin/sqlservr