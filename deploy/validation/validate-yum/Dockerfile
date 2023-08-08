FROM rockylinux:9.2.20230513@sha256:b07e21a7bbcecbae55b9153317d333d4d50808bf5dc0859db0180b6fbd7afb3d

RUN yum install wget -y \
    && wget https://download.newrelic.com/548C16BF.gpg -O /etc/pki/rpm-gpg/RPM-GPG-KEY-NewRelic \
    && rpm --import /etc/pki/rpm-gpg/RPM-GPG-KEY-NewRelic

COPY --chmod=777 check-version.sh /tmp/

ENTRYPOINT ["/tmp/check-version.sh"]
