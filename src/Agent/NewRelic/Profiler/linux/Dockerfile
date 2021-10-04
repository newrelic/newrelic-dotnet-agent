# This builds an Ubuntu image, clones the coreclr github repo and builds it.
# It then sets up the environment for compiling the New Relic .NET profiler.
FROM ubuntu:14.04

RUN apt-get update && apt-get install -y \
  wget \
  curl \
  git \
  dos2unix \
  software-properties-common

RUN echo "deb https://apt.llvm.org/trusty/ llvm-toolchain-trusty-3.9 main" | tee /etc/apt/sources.list.d/llvm.list
RUN wget -O - https://apt.llvm.org/llvm-snapshot.gpg.key | apt-key add -

# The CoreCLR build notes say their repos should be pulled into a `git` directory.
# That probably isn't necessary, but whatever.
RUN mkdir /root/git
WORKDIR /root/git

RUN git clone --branch release/3.0 https://github.com/dotnet/coreclr.git

# Install the build tools that the profiler requires
RUN apt-get update && apt-get install -y \
  make \
  binutils \
  libc++-dev \
  clang-3.9 \
  lldb-3.9

# Install cmake 3.9
RUN curl -sSL https://cmake.org/files/v3.9/cmake-3.9.0-rc3-Linux-x86_64.tar.gz | tar -xzC /opt
RUN ln -s /opt/cmake-3.9.0-rc3-Linux-x86_64/bin/cmake /usr/local/sbin/cmake

RUN rm /usr/bin/cc;   ln -s /usr/bin/clang-3.9 /usr/bin/cc
RUN rm /usr/bin/c++;  ln -s /usr/bin/clang++-3.9 /usr/bin/c++
