

FROM ubuntu:jammy

RUN apt-get update && DEBIAN_FRONTEND=noninteractive apt-get install -y --no-install-recommends \
# git + root certificates to checkout builder code
    git ca-certificates \
# rpm; to build rpm
    rpm \
# gnupg2; to sign packages
    gnupg2 \
# python3 and python3-gnupg; to import signing key
    python3 python3-gnupg \
    dos2unix \
    expect \
# save on final image size
 && rm -rf /var/lib/apt/lists/*

# Install New Relic's public key to verify RPM signature
RUN apt-get update && apt-get install -y --no-install-recommends \
# wget to download New Relic's public key
    wget \
 && wget -O /tmp/newrelic-public.gpg https://download.newrelic.com/548C16BF.gpg \
 && rpm --import /tmp/newrelic-public.gpg \
# cleanup temporary state to save on final image size
 && apt-get autoremove -y wget \
 && rm -rf /var/lib/apt/lists/*

 # Install exiftool
RUN git clone https://github.com/exiftool/exiftool.git