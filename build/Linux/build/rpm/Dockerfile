

FROM ubuntu:noble-20260911@sha256:6896323949d7acdf78c9fa087b858849920791181bc34eb41d432005cfdcd5d7

# archive.ubuntu.com round-robins over hosts that intermittently refuse port 80
RUN printf 'Acquire::Retries "5";\nAcquire::http::Timeout "20";\n' > /etc/apt/apt.conf.d/99-nr-retries \
 && sed -i 's|http://archive\.ubuntu\.com/ubuntu|http://azure.archive.ubuntu.com/ubuntu|g' /etc/apt/sources.list.d/ubuntu.sources \
 && apt-get update && DEBIAN_FRONTEND=noninteractive apt-get install -y --no-install-recommends \
    rpm \
    gnupg2 \
    dos2unix \
    wget \
#to fetch New Relic's public key without certificate issues
    ca-certificates \
# to clone exiftool repository
    git \
# save on final image size
 && rm -rf /var/lib/apt/lists/*

# Install New Relic's public key to verify RPM signature
RUN wget -O /tmp/newrelic-public.gpg https://download.newrelic.com/NEWRELIC_APT_2DAD550E.public \
 && rpm --import /tmp/newrelic-public.gpg

 # Install exiftool
RUN git clone https://github.com/exiftool/exiftool.git