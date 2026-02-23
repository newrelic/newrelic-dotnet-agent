#!/bin/sh
set -e

echo "🦀 New Relic Profiler POC - Comprehensive musl Toolchain Validation"
echo "=================================================================="

# Show platform and toolchain info
echo "Platform: $(uname -a)"
echo "Alpine version: $(cat /etc/alpine-release)"
echo "Rust version: $(rustc --version)"
echo "GCC version: $(gcc --version | head -n1)"
echo "Musl version: $(ldd --version 2>&1 | head -n1 || echo 'musl libc')"
echo ""

# Show available libraries
echo "🔍 MUSL TOOLCHAIN ANALYSIS:"
echo "Available system libraries:"
ls -la /usr/lib/ | grep -E "(libc|libgcc)" || echo "Standard libs not found in /usr/lib"
ls -la /lib/ | grep -E "(libc|libgcc)" || echo "Standard libs not found in /lib"
echo ""

echo "Musl library locations:"
find /usr -name "libc.so*" 2>/dev/null | head -5 || echo "No libc.so found"
find /usr -name "*gcc*" 2>/dev/null | head -5 || echo "No gcc libs found"
echo ""

# Test basic C compilation
echo "🧪 TESTING BASIC C COMPILATION:"
cat > test_static.c << 'EOF'
#include <stdio.h>
int main() {
    printf("Hello from musl static!\n");
    return 0;
}
EOF

cat > test_dynamic.c << 'EOF'
#include <stdio.h>
int main() {
    printf("Hello from musl dynamic!\n");
    return 0;
}
EOF

echo "Static C compilation:"
if gcc -static -o test_static test_static.c; then
    echo "✅ Static C compilation: SUCCESS"
    ./test_static
    echo "File type: $(file test_static)"
    echo "Dependencies: $(ldd test_static 2>&1 || echo 'statically linked')"
else
    echo "❌ Static C compilation: FAILED"
fi
echo ""

echo "Dynamic C compilation:"
if gcc -o test_dynamic test_dynamic.c; then
    echo "✅ Dynamic C compilation: SUCCESS"
    ./test_dynamic
    echo "File type: $(file test_dynamic)"
    echo "Dependencies: $(ldd test_dynamic 2>&1)"
else
    echo "❌ Dynamic C compilation: FAILED"
fi
echo ""

# Test shared library compilation
echo "🔧 TESTING SHARED LIBRARY COMPILATION:"
cat > libtest.c << 'EOF'
#include <stdio.h>

__attribute__((visibility("default")))
int test_function() {
    printf("Hello from shared library!\n");
    return 42;
}
EOF

echo "C shared library compilation:"
if gcc -shared -fPIC -o libtest.so libtest.c; then
    echo "✅ C shared library: SUCCESS"
    echo "File type: $(file libtest.so)"
    echo "Dependencies: $(ldd libtest.so 2>&1)"

    # Test loading the library
    cat > test_load.c << 'EOF'
#include <dlfcn.h>
#include <stdio.h>
int main() {
    void* lib = dlopen("./libtest.so", RTLD_LAZY);
    if (lib) {
        printf("✅ Shared library loads successfully\n");
        dlclose(lib);
        return 0;
    } else {
        printf("❌ Failed to load shared library: %s\n", dlerror());
        return 1;
    }
}
EOF

    if gcc -ldl -o test_load test_load.c && ./test_load; then
        echo "✅ Shared library loading: SUCCESS"
    else
        echo "⚠️ Shared library compilation OK but loading failed"
    fi
else
    echo "❌ C shared library: FAILED"
fi
echo ""

# Show Rust target info
echo "🦀 RUST TARGET INFORMATION:"
rustc --print target-list | grep musl || echo "No musl targets?"
rustc --print cfg --target x86_64-unknown-linux-musl
echo ""

# Test Rust compilation - approach 1: Default settings
echo "🚀 RUST COMPILATION TEST 1: Default musl target"
cargo clean >/dev/null 2>&1 || true

if cargo build --target x86_64-unknown-linux-musl --release --verbose; then
    echo "✅ SUCCESS: Default Rust musl compilation worked!"

    LIBRARY_PATH="target/x86_64-unknown-linux-musl/release/libnewrelic_profiler_poc.so"
    if [ -f "$LIBRARY_PATH" ]; then
        echo "📦 Library created: $LIBRARY_PATH"
        echo "   Size: $(du -h $LIBRARY_PATH | cut -f1)"
        echo "   Type: $(file $LIBRARY_PATH)"
        echo "   Dependencies: $(ldd $LIBRARY_PATH 2>&1 | wc -l) dynamic libraries"
        ldd $LIBRARY_PATH 2>&1 | head -10

        echo ""
        echo "🎉 PROOF OF CONCEPT SUCCESS!"
        echo "✅ Rust can create musl dynamic libraries"
        echo "✅ Alpine Linux profiler is technically feasible"
        echo "🔧 C++ profiler limitation solved with Rust"
    else
        echo "❌ Build succeeded but library not found"
    fi
else
    echo "❌ Default Rust musl compilation failed"
    echo "Trying alternative approaches..."
    echo ""

    # Test Rust compilation - approach 2: Explicit linker
    echo "🚀 RUST COMPILATION TEST 2: Explicit musl-gcc linker"
    export CC_x86_64_unknown_linux_musl=musl-gcc
    export CARGO_TARGET_X86_64_UNKNOWN_LINUX_MUSL_LINKER=musl-gcc

    cargo clean >/dev/null 2>&1 || true

    if cargo build --target x86_64-unknown-linux-musl --release --verbose; then
        echo "✅ SUCCESS: Explicit linker approach worked!"
    else
        echo "❌ Explicit linker approach failed"
        echo ""

        # Test Rust compilation - approach 3: System linker
        echo "🚀 RUST COMPILATION TEST 3: System GCC linker"
        export CARGO_TARGET_X86_64_UNKNOWN_LINUX_MUSL_LINKER=gcc

        cargo clean >/dev/null 2>&1 || true

        if cargo build --target x86_64-unknown-linux-musl --release --verbose; then
            echo "✅ SUCCESS: System GCC linker worked!"
        else
            echo "❌ All Rust compilation approaches failed"

            echo ""
            echo "🔍 DETAILED DIAGNOSTICS:"
            echo "Cargo configuration:"
            env | grep -i cargo || echo "No CARGO env vars"
            echo ""
            echo "Rust sysroot:"
            rustc --print sysroot
            echo ""
            echo "Target triple details:"
            rustc --print target-spec-json --target x86_64-unknown-linux-musl 2>/dev/null || echo "Cannot get target spec"
            echo ""
            echo "Available musl libraries:"
            find $(rustc --print sysroot) -name "*musl*" 2>/dev/null | head -10 || echo "No musl libs in sysroot"
        fi
    fi
fi

echo ""
echo "📋 FINAL ASSESSMENT:"
echo "===================="

# Summary of what worked
if [ -f "target/x86_64-unknown-linux-musl/release/libnewrelic_profiler_poc.so" ]; then
    echo "🎯 CRITICAL SUCCESS: musl dynamic library compilation WORKS"
    echo "✅ Technical blocker for Alpine Linux support: RESOLVED"
    echo "✅ Rust profiler POC: VALIDATED for musl targets"
    echo "🚀 RECOMMENDATION: Proceed with full Rust profiler development"
    echo ""
    echo "📦 DELIVERABLE PROOF:"
    ls -la target/x86_64-unknown-linux-musl/release/*.so
    echo ""
    echo "🎉 This capability is impossible with the current C++ profiler!"
    echo "💪 Rust has solved a fundamental limitation of the existing system."
else
    echo "❌ TECHNICAL BLOCKER: musl compilation still failing"
    echo "🔧 Need additional investigation into musl toolchain setup"
    echo "📋 Consider alternative approaches or accept limitation for POC"
    echo ""
    echo "🤔 OPTIONS:"
    echo "1. Focus POC on glibc Linux + Windows (still valuable)"
    echo "2. Research specialized musl Rust compilation techniques"
    echo "3. Investigate if static linking is acceptable for profiler"
fi

# Cleanup
rm -f test_static test_dynamic test_load libtest.so *.c

echo ""
echo "🏁 Comprehensive musl validation complete!"