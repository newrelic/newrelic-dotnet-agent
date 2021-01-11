FROM microsoft/mssql-server-linux:2017-latest

ENV ACCEPT_EULA=Y
ENV SA_PASSWORD=password0TS

COPY setup.sh /var/
COPY entrypoint.sh /var/
COPY restore.sql /var/

# Copy database backup file to container
COPY NewRelicDB.bak /var/opt/mssql/backup/NewRelicDB.bak

RUN chmod 777 /var/setup.sh \
   && chmod 777 /var/entrypoint.sh

ENTRYPOINT ["/var/entrypoint.sh"]