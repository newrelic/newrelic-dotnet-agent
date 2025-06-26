FROM mysql:lts

# Add custom configuration for UTF-8
COPY my.cnf /etc/mysql/conf.d/
COPY database.sql /docker-entrypoint-initdb.d