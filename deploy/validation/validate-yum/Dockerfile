FROM rockylinux:9

RUN yum install wget -y \
    && wget https://download.newrelic.com/548C16BF.gpg -O /etc/pki/rpm-gpg/RPM-GPG-KEY-NewRelic \
    && rpm --import /etc/pki/rpm-gpg/RPM-GPG-KEY-NewRelic

COPY --chmod=777 check-version.sh /tmp/

ENTRYPOINT ["/tmp/check-version.sh"]
