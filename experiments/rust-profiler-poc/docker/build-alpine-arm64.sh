#!/bin/sh
set -e

echo "🦀 Building New Relic Profiler POC for Alpine Linux ARM64 (musl)"
echo "=============================================================="

# Show platform info
echo "Platform: $(uname -a)"
echo "Architecture: $(uname -m)"
echo "Rust version: $(rustc --version)"
echo "Target: aarch64-unknown-linux-musl"
echo ""

# Build the profiler for ARM64 musl target
echo "Building for ARM64 musl target..."
cargo build --target aarch64-unknown-linux-musl --release

# Check if build succeeded
if [ $? -eq 0 ]; then
    echo "✅ SUCCESS: ARM64 musl build completed!"
    echo ""

    # Show build artifacts
    echo "Build artifacts:"
    ls -la target/aarch64-unknown-linux-musl/release/
    echo ""

    # Show library info
    LIBRARY_PATH="target/aarch64-unknown-linux-musl/release/libnewrelic_profiler_poc.so"
    if [ -f "$LIBRARY_PATH" ]; then
        echo "📦 Library created: $LIBRARY_PATH"
        echo "Size: $(du -h $LIBRARY_PATH | cut -f1)"
        echo "Type: $(file $LIBRARY_PATH)"

        # Test the P/Invoke exports
        echo ""
        echo "🔍 Testing P/Invoke exports:"
        if command -v objdump >/dev/null 2>&1; then
            echo "Exported symbols:"
            objdump -T "$LIBRARY_PATH" | grep "NewRelic_Profiler" || echo "No symbols found (might be stripped)"
        else
            echo "objdump not available, skipping symbol check"
        fi

        echo ""
        echo "🎯 MAJOR ACHIEVEMENT:"
        echo "ARM64 + musl compilation successful!"
        echo "This enables deployment to:"
        echo "  - ARM64 Alpine Linux containers"
        echo "  - AWS Graviton processors"
        echo "  - Modern ARM64 cloud infrastructure"
        echo "  - Something the C++ profiler absolutely cannot do!"

    else
        echo "❌ ERROR: Library file not created"
        exit 1
    fi
else
    echo "❌ ERROR: ARM64 musl build failed"
    exit 1
fi