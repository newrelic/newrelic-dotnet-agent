FROM debian:stable-20260824@sha256:7fa362f5e7c036d7f45621a6c721c03493cdcb5889adfcd65587759e34fe4c6f

RUN apt-get update && apt-get install -y \
    apt-utils \
    dpkg-dev \
    createrepo-c \
    awscli \
    curl \
    dos2unix \
    bsdmainutils \
    rsync \
    gnupg2 \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /data

COPY deploy.bash .

COPY ./deploy_scripts /data/deploy_scripts

COPY ./packages /packages

RUN dos2unix deploy.bash && chmod a+x deploy.bash
RUN find /data/deploy_scripts -type f |xargs dos2unix
RUN find /data/deploy_scripts -name "*.bash" |xargs chmod a+x
