FROM mcr.microsoft.com/mssql/server:2022-latest

USER root
COPY setup.sh /var/
COPY entrypoint.sh /var/
COPY restore.sql /var/
# Copy database backup file to container
COPY NewRelicDB.bak /var/opt/mssql/backup/NewRelicDB.bak
RUN chmod 777 /var/setup.sh \
   && chmod 777 /var/entrypoint.sh

USER mssql
ENTRYPOINT ["/var/entrypoint.sh"]