# sandbox image with travel-sample bucket pre-installed
FROM couchbase/server-sandbox:7.6.5  

COPY configure-server.sh /opt/couchbase
RUN chmod +x /opt/couchbase/configure-server.sh
CMD ["/opt/couchbase/configure-server.sh"]