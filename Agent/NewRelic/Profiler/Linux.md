## Linux Load Failure

Our profiler fails to load on Linux.  CoreCLR tries to load our dll with `dlopen` in a method named `LOADLoadLibraryDirect`.  The dll fails to load with the error message `undefined symbol: _ZNKSt3__119__shared_weak_count13__get_deleterERKSt9type_info"`.

UPDATE - the build was compiling with the correct standard library but linking the incorrect one.

## Reproducing the issue

Build the profiler.

    linux/full_linux_build.sh

Open a bash shell in the container.

    docker run --privileged=true -v $PWD/..:/profiler -i -t profiler_build /bin/bash

Create a new application in the binary output directory of the coreclr and run `lldb`

    cd $CORECLR_BINARIES/
    dotnet new console && dotnet build
    ./debug.sh

In lldb, continue 3 times, then step past the `dlopen` call for our profiler.

     c
     <hit enter>
     <hit enter>
     s
     s
     s

Print out the value returned by invoking `dlerror()`

    expr (char*) dlerror()
