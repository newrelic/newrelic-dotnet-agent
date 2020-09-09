FROM microsoft/mssql-server-linux:2017-latest

ENV ACCEPT_EULA=Y
ENV MSSQL_SA_PASSWORD=password0TS
ENV SQLCMDPASSWORD=password0TS
ENV SA_PASSWORD=password0TS

COPY setup.sh /var/
COPY entrypoint.sh /var/
COPY restore.sql /var/

# Try restoring from backup of NewRelic database on dn-database-01
COPY NewRelicDB.bak /var/opt/mssql/backup/NewRelicDB.bak

RUN chmod 777 /var/setup.sh \
   && chmod 777 /var/entrypoint.sh

ENTRYPOINT ["/var/entrypoint.sh"]