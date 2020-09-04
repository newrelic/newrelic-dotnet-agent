FROM microsoft/mssql-server-linux:2017-latest

ENV ACCEPT_EULA=true
ENV MSSQL_SA_PASSWORD=password0TS
ENV SQLCMDPASSWORD=password0TS
ENV SA_PASSWORD=password0TS

# Try restoring from backup of NewRelic database on dn-database-01
COPY NewRelicDB.bak /var/opt/mssql/backup/NewRelicDB.bak

RUN /opt/mssql-tools/bin/sqlcmd \
   -S localhost\SQLExpress -U SA -P password \
   -Q 'RESTORE DATABASE NewRelic FROM DISK = "/var/opt/mssql/backup/NewRelicDB.bak"'

# Try re-creating DB from .mdf file from dn-database-01
# COPY NewRelic.mdf /var/opt/mssql/data
# <insert appropriate sqlcmd here>
