FROM debian:stable-20231120@sha256:6a798ffaa39776765d80c63afffc5920d09f8003b1b7d6a24026527d826c8de7

RUN apt-get update && apt-get install -y \
    apt-utils \
    dpkg-dev \
    createrepo-c \
    awscli \
    curl \
    dos2unix \
    bsdmainutils \
    rsync \
    gnupg1 \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /data

COPY deploy.bash .

COPY ./deploy_scripts /data/deploy_scripts

COPY ./packages /packages

RUN dos2unix deploy.bash && chmod a+x deploy.bash
RUN find /data/deploy_scripts -type f |xargs dos2unix
RUN find /data/deploy_scripts -name "*.bash" |xargs chmod a+x
