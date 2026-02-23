# Cross-compile to musl from glibc environment
FROM ubuntu:22.04

# Install Rust and cross-compilation tools for musl
RUN apt-get update && apt-get install -y \
    curl \
    gcc \
    musl-tools \
    && curl --proto '=https' --tlsv1.2 -sSf https://sh.rustup.rs | sh -s -- -y \
    && . $HOME/.cargo/env \
    && rustup target add x86_64-unknown-linux-musl \
    && rustup target add aarch64-unknown-linux-musl \
    && apt-get clean \
    && rm -rf /var/lib/apt/lists/*

# Set up environment
ENV PATH="/root/.cargo/bin:${PATH}"
ENV CC_x86_64_unknown_linux_musl=musl-gcc
ENV CC_aarch64_unknown_linux_musl=aarch64-linux-musl-gcc

# Set working directory
WORKDIR /workspace

# Copy the entire project
COPY . /workspace/

# Build script
COPY docker/build-cross-musl.sh /usr/local/bin/build-cross-musl.sh
RUN chmod +x /usr/local/bin/build-cross-musl.sh

# Default command
CMD ["/usr/local/bin/build-cross-musl.sh"]