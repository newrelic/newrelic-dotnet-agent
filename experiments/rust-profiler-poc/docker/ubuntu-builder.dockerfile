# Ubuntu Linux (glibc) builder - Standard Linux distribution
FROM ubuntu:22.04

# Install Rust and build dependencies
RUN apt-get update && apt-get install -y \
    curl \
    gcc \
    libc6-dev \
    && curl --proto '=https' --tlsv1.2 -sSf https://sh.rustup.rs | sh -s -- -y \
    && . $HOME/.cargo/env \
    && rustup target add x86_64-unknown-linux-gnu \
    && apt-get clean \
    && rm -rf /var/lib/apt/lists/*

# Set up environment
ENV PATH="/root/.cargo/bin:${PATH}"

# Set working directory
WORKDIR /workspace

# Build script
COPY docker/build-ubuntu.sh /usr/local/bin/build-ubuntu.sh
RUN chmod +x /usr/local/bin/build-ubuntu.sh

# Default command
CMD ["/usr/local/bin/build-ubuntu.sh"]