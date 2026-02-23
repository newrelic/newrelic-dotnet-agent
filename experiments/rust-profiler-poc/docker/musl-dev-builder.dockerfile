# Comprehensive musl development environment for dynamic library builds
FROM alpine:3.19

# Install complete musl development toolchain
RUN apk add --no-cache \
    # Core build tools
    build-base \
    curl \
    wget \
    git \
    # Musl development essentials
    musl-dev \
    musl-utils \
    # GCC and toolchain
    gcc \
    g++ \
    gdb \
    make \
    cmake \
    # Additional libraries that might be needed
    linux-headers \
    libgcc \
    # Debugging tools (correct Alpine package names)
    file \
    binutils

# Install Rust with musl target support
RUN curl --proto '=https' --tlsv1.2 -sSf https://sh.rustup.rs | sh -s -- -y --default-toolchain stable \
    && source $HOME/.cargo/env \
    && rustup target add x86_64-unknown-linux-musl

# Set up environment for musl builds
ENV PATH="/root/.cargo/bin:${PATH}"
ENV CC=gcc
ENV CXX=g++
ENV AR=ar
ENV RANLIB=ranlib

# Verify musl toolchain is working
RUN gcc --version && \
    ld --version && \
    echo "int main(){return 0;}" > test.c && \
    gcc -static -o test test.c && \
    ./test && \
    rm test test.c

WORKDIR /workspace

# Copy project files
COPY . /workspace/

# Build script for comprehensive testing
COPY docker/build-musl-comprehensive.sh /usr/local/bin/build-musl-comprehensive.sh
RUN chmod +x /usr/local/bin/build-musl-comprehensive.sh

CMD ["/usr/local/bin/build-musl-comprehensive.sh"]