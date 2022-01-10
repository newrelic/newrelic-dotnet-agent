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
  make \
  binutils \
  cmake \
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

  # LLVM
RUN apt-get install -y libllvm-13-ocaml-dev libllvm13 llvm-13 llvm-13-dev llvm-13-doc llvm-13-examples llvm-13-runtime \
# Clang and co
clang-13 clang-tools-13 clang-13-doc libclang-common-13-dev libclang-13-dev libclang1-13 clang-format-13 python3-clang-13 clangd-13 \
# lldb, lld, libc++, libunwind
lldb-13 lld-13 libc++-13-dev libc++abi-13-dev libunwind-13-dev

# The CoreCLR build notes say their repos should be pulled into a `git` directory.
# Not sure how necessary that is.
RUN mkdir /root/git
WORKDIR /root/git

RUN git clone --branch release/3.1 https://github.com/dotnet/coreclr.git

RUN ln -sf /usr/bin/clang-13 /usr/bin/cc; ln -sf /usr/bin/clang++-13 /usr/bin/c++
