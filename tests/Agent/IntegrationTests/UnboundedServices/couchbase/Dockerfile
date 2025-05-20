# sandbox image with travel-sample bucket pre-installed
FROM couchbase/server-sandbox:7.6.6@sha256:584258b112afa1512baa11e272c904c7cf8ffa35b99ab02fa6ff87942a60e4a3

COPY configure-server.sh /opt/couchbase

# Add analytics service to the server
RUN chmod +x /opt/couchbase/configure-server.sh && \
    sed -i "/setupServices/s/$/%2Ccbas/" /etc/service/config-couchbase/run

CMD ["/opt/couchbase/configure-server.sh"]