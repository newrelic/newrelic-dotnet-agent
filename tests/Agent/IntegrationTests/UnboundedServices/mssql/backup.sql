USE [master]
GO
BACKUP DATABASE [NewRelic]
TO DISK = '/var/opt/mssql/backup/NewRelicDB.bak'
WITH
INIT;
GO