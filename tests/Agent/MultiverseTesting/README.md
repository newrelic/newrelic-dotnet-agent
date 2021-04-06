# Multiverse Testing

This tool is used to test the instrumentation points in our instrumentation XML files against the methods signatures in the relevant packages we instrument.

## Why are doing this

While exist instrumentation changes very infrequently, the same is not true for the assemblies we are instrumenting.  As they mature their designers are changing the APIs that we are expecting.  For public APIs, this is not usually a big issue since these types of changes should only occur during a major version bump (for those following SemVar).  We don't just instrument public API since those often are too high level to be useful for instrumentation.  Instead,we will dig into the assemblies and find lower level private methods that provide larger amounts of coverage or access to data we need to build metrics.  These can and do change as often as the developers need them to since they are not publicly accessible. This tool will give us a way to track those changes over time and ensure that are correct in what we state we support.

## How do we do this

We use Mono.Cecil to inspect the assemblies, without having to acquire their dependecies, to build a list of method signatures.  At the same time we read our instrumentation XML files and build a set of instrumentation points -- method signatures -- to check.  We compare the two to see if we find the expected instrumentation point exists.  If it does, we can reasonably assume that we will be able instrument that method, if not we know for sure that we cannot instrument that method.

