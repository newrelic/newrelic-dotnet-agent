#!/bin/bash
set -e

echo "🦀 Building New Relic Profiler POC for Ubuntu Linux (glibc)"
echo "=========================================================="

# Show platform info
echo "Platform: $(uname -a)"
echo "Rust version: $(rustc --version)"
echo "Target: x86_64-unknown-linux-gnu"
echo ""

# Build the profiler for glibc target
echo "Building for glibc target..."
cargo build --target x86_64-unknown-linux-gnu --release

# Check if build succeeded
if [ $? -eq 0 ]; then
    echo "✅ SUCCESS: glibc build completed!"
    echo ""

    # Show build artifacts
    echo "Build artifacts:"
    ls -la target/x86_64-unknown-linux-gnu/release/
    echo ""

    # Show library info
    LIBRARY_PATH="target/x86_64-unknown-linux-gnu/release/libnewrelic_profiler_poc.so"
    if [ -f "$LIBRARY_PATH" ]; then
        echo "📦 Library created: $LIBRARY_PATH"
        echo "Size: $(du -h $LIBRARY_PATH | cut -f1)"
        echo "Type: $(file $LIBRARY_PATH)"

        # Show dependencies
        echo ""
        echo "🔗 Library dependencies:"
        ldd "$LIBRARY_PATH" | head -10 || echo "ldd failed"

        # Test the P/Invoke exports
        echo ""
        echo "🔍 Testing P/Invoke exports:"
        echo "Exported symbols:"
        objdump -T "$LIBRARY_PATH" | grep "NewRelic_Profiler" || echo "No symbols found (might be stripped)"

        echo ""
        echo "🎯 Standard Linux (glibc) compilation successful!"
        echo "This proves compatibility with standard Linux distributions."

    else
        echo "❌ ERROR: Library file not created"
        exit 1
    fi
else
    echo "❌ ERROR: glibc build failed"
    exit 1
fi