#!/bin/bash
# this script is designed to be run inside of a docker container

cd /profiler
rm -f CMakeCache.txt libNewRelicProfiler.so
cmake \
	-DCORECLR_PATH=/root/git/runtime \
	.
make clean && make 
retVal=$?

if [ -f "libNewRelicProfiler.so" ]
	then ldd libNewRelicProfiler.so
	else 
		echo "::error libNewRelicProfiler.so was not built"
fi

if [ $retVal -ne 0 ]; then
    echo "::error Exit code was $retVal."
fi

exit $retVal