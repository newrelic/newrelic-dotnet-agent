# sandbox image with travel-sample bucket pre-installed
FROM couchbase/server-sandbox:7.6.5  

COPY configure-server.sh /opt/couchbase

# Add analytics service to the server
RUN chmod +x /opt/couchbase/configure-server.sh && \
    sed -i "/setupServices/s/$/%2Ccbas/" /etc/service/config-couchbase/run

CMD ["/opt/couchbase/configure-server.sh"]