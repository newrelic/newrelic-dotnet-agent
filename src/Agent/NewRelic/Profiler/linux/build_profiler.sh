#!/bin/bash
# this script is designed to be run inside of a docker container

cd /profiler
rm -f CMakeCache.txt libNewRelicProfiler.so
cmake .
make clean && make
retVal=$?

if [ -f "libNewRelicProfiler.so" ]
	then ldd libNewRelicProfiler.so

		# ELF allows a shared object to link with unresolved symbols, so an ODR-use of a
		# declaration-only member (e.g. a pre-C++17 `static constexpr` bound to a reference)
		# or a missing out-of-line virtual destructor links clean and then fails the CLR's
		# dlopen with "undefined symbol", leaving the profiler silently unattached. Data
		# symbols cannot bind lazily, so -z lazy does not save us, and -z defs cannot catch it
		# because RaiseException is legitimately undefined here (the CLR supplies it at
		# runtime). So check our own symbols directly: nothing first-party should ever be left
		# undefined.
		#
		# Matching demangled names keeps every mangled spelling in scope -- plain symbols,
		# const members, vtables, typeinfo, guard variables, thunks -- while anchoring on the
		# root ("X..." or "... for X" / "... to X") ignores std:: templates that merely mention
		# our types. std:: is excluded deliberately: the arm64 build leaves std:: symbols
		# undefined exactly as the long-shipping arm64 binary does, a separate pre-existing
		# condition rather than this bug class. The symbol-type column is matched against
		# U/w/v, not just U: vague linkage (template instantiations, inline functions, weak
		# vtables) surfaces as a lowercase weak-undefined type, and a miss there still fails
		# dlopen with "undefined symbol" at runtime just like a plain U would.
		if ! undefined_symbols=$(nm -DC --undefined-only libNewRelicProfiler.so)
			then
				echo "::error could not read symbols from libNewRelicProfiler.so"
				exit 1
		fi

		undefined_first_party=$(printf '%s\n' "$undefined_symbols" | sed -n 's/^ *[Uwv] //p' | grep -E '(^|for |to )(NewRelic|sicily)::')
		if [ -n "$undefined_first_party" ]
			then
				echo "::error libNewRelicProfiler.so has undefined first-party symbols and will fail to load:"
				printf '%s\n' "$undefined_first_party"
				retVal=1
		fi
	else
		echo "::error libNewRelicProfiler.so was not built"
fi

if [ $retVal -ne 0 ]; then
    echo "::error Exit code was $retVal."
fi

exit $retVal