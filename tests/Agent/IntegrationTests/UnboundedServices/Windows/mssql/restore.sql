USE [master]
GO
RESTORE DATABASE [NewRelic]
FROM DISK = 'c:\NewRelicDB.bak'
WITH
MOVE 'NewRelic' TO 'c:\NewRelic.mdf',
MOVE 'NewRelic_log' TO 'c:\NewRelic_log.ldf';
GO