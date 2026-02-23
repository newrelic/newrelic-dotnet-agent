# Alpine Linux (musl) builder - KEY VALUE: musl support that C++ can't do
FROM alpine:latest

# Install Rust and build dependencies for musl target
RUN apk add --no-cache \
    curl \
    gcc \
    musl-dev \
    musl-utils \
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
COPY docker/build-alpine.sh /usr/local/bin/build-alpine.sh
RUN chmod +x /usr/local/bin/build-alpine.sh

# Default command
CMD ["/usr/local/bin/build-alpine.sh"]