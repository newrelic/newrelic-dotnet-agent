# start SQL Server - start the script to restore the DB
# SQL startup MUST be at the end of this line to keep container up

Write-Output "entrypoint.ps1"

.\setup.ps1 
Start-Process .\Program Files\Microsoft SQL Server\MSSQL14.SQLEXPRESS\MSSQL\Binn\sqlservr.exe
