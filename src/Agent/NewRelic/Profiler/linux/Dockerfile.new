# This builds an Ubuntu image, clones the coreclr github repo and builds it.
# It then sets up the environment for compiling the New Relic .NET profiler.

# **WARNING** this will not build a profiler that works on Alpine Linux.
# See https://github.com/newrelic/newrelic-dotnet-agent/issues/918
FROM ubuntu:18.04

RUN apt-get update
RUN apt-get install -y \
  wget \
  curl \
  git \
  dos2unix \
  gnupg
  
RUN echo "deb http://apt.llvm.org/bionic/ llvm-toolchain-bionic-7 main" | tee /etc/apt/sources.list.d/llvm.list
RUN wget -O - https://apt.llvm.org/llvm-snapshot.gpg.key | apt-key add -
RUN apt-get update

# Putting this on it's own line, tzdata is a dependency of one of the packages being installed below
# and it needs to be told what timezone it is in.  Just use UTC.
RUN DEBIAN_FRONTEND=noninteractive TZ=Etc/UTC apt-get -y install tzdata

RUN apt-get install -y \
  cmake llvm-7 clang-7 lldb-7 liblldb-7-dev libunwind8 libunwind8-dev gettext libicu-dev liblttng-ust-dev libcurl4-openssl-dev libssl-dev libnuma-dev libkrb5-dev \
  libc++-dev \
  uuid-dev \
  zlib1g-dev \
  locales \
  locales-all

# The CoreCLR build notes say their repos should be pulled into a `git` directory.
# Not sure how necessary that is.
RUN mkdir /root/git
WORKDIR /root/git

RUN git clone --branch release/3.1 https://github.com/dotnet/coreclr.git

RUN ln -sf /usr/bin/clang-7 /usr/bin/cc; ln -sf /usr/bin/clang++-7 /usr/bin/c++
