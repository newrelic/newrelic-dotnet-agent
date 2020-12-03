USE [master]
GO
RESTORE DATABASE [NewRelic]
FROM DISK = '/var/opt/mssql/backup/NewRelicDB.bak'
WITH
MOVE 'NewRelic' TO '/tmp/NewRelic.mdf',
MOVE 'NewRelic_log' TO '/tmp/NewRelic_log.ldf';
GO