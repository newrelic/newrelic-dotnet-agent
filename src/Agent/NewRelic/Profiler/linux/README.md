## Linux profiler build Dockerfiles

On this experimental branch: 

`Dockerfile` is a build container that uses more up-to-date Ubuntu and llvm versions and works to build the profiler for
`glibc`-based Linux distros like Ubuntu and Centos.  However, the profiler built from this container will not work on `musl`-based
Linux distros like Alpine.

`Dockerfile.old` is currently "old" Linux profiler build container that uses very out-of-date Ubuntu and llvm toolchain versions.
However, it still works to build the profiler for Linux, including Alpine Linux.

`Dockerfile.debug` is like `Dockerfile.new` but does some extra things to support debugging the profiler, but this was copied from
an old process and it is a work in progress.

We want to eventually figure out how to build the profiler using the newer llvm toolchain in a way that works on all Linux versions
supported by Microsoft for .NET, including Alpine.

Information correct as of 2023-12-14.