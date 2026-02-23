# Alpine Linux ARM64 (musl) builder - ARM64 + musl combination
FROM --platform=linux/arm64 alpine:latest

# Install Rust and build dependencies for musl target
RUN apk add --no-cache \
    curl \
    gcc \
    musl-dev \
    && curl --proto '=https' --tlsv1.2 -sSf https://sh.rustup.rs | sh -s -- -y \
    && source $HOME/.cargo/env \
    && rustup target add aarch64-unknown-linux-musl

# Set up environment
ENV PATH="/root/.cargo/bin:${PATH}"
ENV CC_aarch64_unknown_linux_musl=musl-gcc
ENV CARGO_TARGET_AARCH64_UNKNOWN_LINUX_MUSL_LINKER=musl-gcc

# Set working directory
WORKDIR /workspace

# Build script
COPY docker/build-alpine-arm64.sh /usr/local/bin/build-alpine-arm64.sh
RUN chmod +x /usr/local/bin/build-alpine-arm64.sh

# Default command
CMD ["/usr/local/bin/build-alpine-arm64.sh"]