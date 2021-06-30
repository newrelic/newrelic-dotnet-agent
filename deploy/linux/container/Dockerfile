FROM debian:buster-20210511-slim

RUN apt-get update && apt-get install -y \
    apt-utils \
    dpkg-dev \
    createrepo \
    awscli \
    curl \
    dos2unix \
    bsdmainutils \
    rsync \
    gnupg1 \
    && rm -rf /var/lib/apt/lists/*
