# This builds an Ubuntu image, clones the coreclr github repo and builds it.
# It then sets up the environment for compiling the New Relic .NET profiler.

# **WARNING** this will not build a profiler that works on Alpine Linux.
# See https://github.com/newrelic/newrelic-dotnet-agent/issues/918
FROM ubuntu:22.04

RUN apt-get update
RUN apt-get install -y \
  wget \
  curl \
  git \
  dos2unix \
  gnupg
  

RUN echo "deb http://apt.llvm.org/jammy llvm-toolchain-jammy-17 main" | tee /etc/apt/sources.list.d/llvm.list
RUN wget -O - https://apt.llvm.org/llvm-snapshot.gpg.key | apt-key add -
RUN apt-get update

# Putting this on it's own line, tzdata is a dependency of one of the packages being installed below
# and it needs to be told what timezone it is in.  Just use UTC.
RUN DEBIAN_FRONTEND=noninteractive TZ=Etc/UTC apt-get -y install tzdata

RUN apt-get install -y cmake llvm-17 clang-17 lldb-17 liblldb-17-dev gettext libicu-dev liblttng-ust-dev libcurl4-openssl-dev libssl-dev libnuma-dev libkrb5-dev libc++-dev uuid-dev zlib1g-dev locales

#RUN apt-get install -y \
#  cmake llvm-17 clang-17 lldb-17 liblldb-17-dev libunwind libunwind-dev gettext libicu-dev liblttng-ust-dev libcurl4-openssl-dev libssl-dev libnuma-dev libkrb5-dev \
#  libc++-dev \
#  uuid-dev \
#  zlib1g-dev \
#  locales \
#  locales-all

# The CoreCLR build notes say their repos should be pulled into a `git` directory.
# Not sure how necessary that is.
RUN mkdir /root/git
WORKDIR /root/git

RUN git clone --branch release/6.0 https://github.com/dotnet/runtime.git

RUN ln -sf /usr/bin/clang-17 /usr/bin/cc; ln -sf /usr/bin/clang++-17 /usr/bin/c++
