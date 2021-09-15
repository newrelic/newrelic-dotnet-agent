FROM mcr.microsoft.com/mssql/server:2017-latest

ARG SA_PASS=MssqlPassw0rd

ENV ACCEPT_EULA=Y
ENV MSSQL_SA_PASSWORD=${SA_PASS}
ENV SQLCMDPASSWORD=${SA_PASS}
ENV SA_PASSWORD=${SA_PASS}

COPY setup.sh /var/
COPY entrypoint.sh /var/
COPY restore.sql /var/

# Copy database backup file to container
COPY NewRelicDB.bak /var/opt/mssql/backup/NewRelicDB.bak

RUN chmod 777 /var/setup.sh \
   && chmod 777 /var/entrypoint.sh

ENTRYPOINT ["/var/entrypoint.sh"]