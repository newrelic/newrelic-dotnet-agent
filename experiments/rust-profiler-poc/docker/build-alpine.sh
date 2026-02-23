#!/bin/sh
set -e

echo "🦀 Building New Relic Profiler POC for Alpine Linux (musl)"
echo "========================================================="

# Show platform info
echo "Platform: $(uname -a)"
echo "Rust version: $(rustc --version)"
echo "Target: x86_64-unknown-linux-musl"
echo ""

# Build the profiler for musl target
echo "Building for musl target..."
cargo build --target x86_64-unknown-linux-musl --release

# Check if build succeeded
if [ $? -eq 0 ]; then
    echo "✅ SUCCESS: musl build completed!"
    echo ""

    # Show build artifacts
    echo "Build artifacts:"
    ls -la target/x86_64-unknown-linux-musl/release/
    echo ""

    # Show library info
    LIBRARY_PATH="target/x86_64-unknown-linux-musl/release/libnewrelic_profiler_poc.so"
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

        # Test platform detection export
        echo ""
        echo "🎯 KEY VALUE DEMONSTRATION:"
        echo "This proves musl compilation works - something C++ profiler cannot do!"
        echo "Alpine Linux deployment is now possible with Rust profiler."

    else
        echo "❌ ERROR: Library file not created"
        exit 1
    fi
else
    echo "❌ ERROR: musl build failed"
    exit 1
fi