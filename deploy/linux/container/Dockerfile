FROM debian:jessie

RUN apt-get update && apt-get install -y \
    apt-utils \
    dpkg-dev \
    createrepo \
    awscli \
    curl \
    dos2unix \
    bsdmainutils \
    rsync \
    && rm -rf /var/lib/apt/lists/*
