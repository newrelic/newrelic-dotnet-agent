# This builds an Ubuntu image, clones the coreclr github repo and builds it.
# It then sets up the environment for compiling the New Relic .NET profiler.
FROM ubuntu:20.04

RUN apt-get update
RUN apt-get install -y \
  wget \
  curl \
  git \
  gnupg \
  dos2unix

RUN echo "deb http://apt.llvm.org/focal/ llvm-toolchain-focal-13 main" | tee /etc/apt/sources.list.d/llvm.list
RUN wget -O - https://apt.llvm.org/llvm-snapshot.gpg.key | apt-key add -
RUN apt-get update

# Putting this on it's own line, tzdata is a dependency of one of the packages being installed below
# and it needs to be told what timezone it is in.  Just use UTC.
RUN DEBIAN_FRONTEND=noninteractive TZ=Etc/UTC apt-get -y install tzdata

RUN apt-get install -y \
  libc++-dev \
  cmake \
  llvm-13 \
  clang \
  lldb-13 \
  libunwind-13 \
  libunwind-13-dev \
  gettext \
  libicu-dev \
  liblttng-ust-dev \
  libcurl4-openssl-dev \
  libssl-dev \
  uuid-dev \
  zlib1g-dev \
  libkrb5-dev \
  locales \
  locales-all

# The CoreCLR build notes say their repos should be pulled into a `git` directory.
# Not sure how necessary that is.
RUN mkdir /root/git
WORKDIR /root/git

RUN git clone --branch release/6.0 https://github.com/dotnet/runtime.git

# Build CoreCLR
RUN cd /root/git/runtime; ./build.sh clr

# Install the build tools that the profiler requires
#RUN apt-get update && apt-get install -y \
#  make \
#  binutils \
#  libc++-dev \
#  clang-3.9 \
#  lldb-3.9

# Remove expired root CA cert
#RUN sed -i 's/mozilla\/DST_Root_CA_X3.crt/!mozilla\/DST_Root_CA_X3.crt/g' /etc/ca-certificates.conf
#RUN update-ca-certificates

# Install cmake 3.9
#RUN curl -sSL https://cmake.org/files/v3.9/cmake-3.9.0-rc3-Linux-x86_64.tar.gz | tar -xzC /opt
#RUN ln -s /opt/cmake-3.9.0-rc3-Linux-x86_64/bin/cmake /usr/local/sbin/cmake

#RUN rm /usr/bin/cc;   ln -s /usr/bin/clang-3.9 /usr/bin/cc
#RUN rm /usr/bin/c++;  ln -s /usr/bin/clang++-3.9 /usr/bin/c++
