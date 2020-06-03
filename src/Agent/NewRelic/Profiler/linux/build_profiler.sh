#!/bin/bash
# this script is designed to be run inside of a docker container

cd /profiler
rm -f CMakeCache.txt libNewRelicProfiler.so
cmake \
	-DCORECLR_PATH=/root/git/coreclr \
	.
make clean && make 

if [ -f "libNewRelicProfiler.so" ]
	then ldd libNewRelicProfiler.so
	else 
		echo "libNewRelicProfiler.so was not built"
fi
