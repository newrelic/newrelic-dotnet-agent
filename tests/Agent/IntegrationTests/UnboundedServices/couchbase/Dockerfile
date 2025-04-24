# sandbox image with travel-sample bucket pre-installed
FROM couchbase/server-sandbox:7.6.5@sha256:daaac2c1291f0ac383fe0daba6c1e8c5b845d3e166e3e252c85bce63c555beb7

COPY configure-server.sh /opt/couchbase

# Add analytics service to the server
RUN chmod +x /opt/couchbase/configure-server.sh && \
    sed -i "/setupServices/s/$/%2Ccbas/" /etc/service/config-couchbase/run

CMD ["/opt/couchbase/configure-server.sh"]