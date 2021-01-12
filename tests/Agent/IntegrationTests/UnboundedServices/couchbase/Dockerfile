FROM couchbase/server:enterprise-4.5.1

RUN apt-get update && apt-get install -y jq

COPY configure-server.sh /opt/couchbase
CMD ["/opt/couchbase/configure-server.sh"]