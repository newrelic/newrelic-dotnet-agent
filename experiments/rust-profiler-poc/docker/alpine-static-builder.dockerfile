# Alpine Linux (musl) static builder - KEY VALUE: musl support with static linking
FROM alpine:latest

# Install Rust and minimal build dependencies
RUN apk add --no-cache \
    curl \
    && curl --proto '=https' --tlsv1.2 -sSf https://sh.rustup.rs | sh -s -- -y \
    && source $HOME/.cargo/env \
    && rustup target add x86_64-unknown-linux-musl

# Set up environment
ENV PATH="/root/.cargo/bin:${PATH}"

# Set working directory
WORKDIR /workspace

# Copy the entire project
COPY . /workspace/

# Build script
COPY docker/build-alpine-static.sh /usr/local/bin/build-alpine-static.sh
RUN chmod +x /usr/local/bin/build-alpine-static.sh

# Default command
CMD ["/usr/local/bin/build-alpine-static.sh"]