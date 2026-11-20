; Unshipped analyzer releases
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
NRDG001 | NewRelic.OtelDistro.Generators | Info | Instrumentation match without a 'parameters' attribute; all overloads with 0-8 parameters are instrumented.
NRDG002 | NewRelic.OtelDistro.Generators | Warning | Instrumentation match has more than 8 parameters; the point is skipped.
NRDG003 | NewRelic.OtelDistro.Generators | Warning | Instrumentation match duplicates another assembly/class/method/parameters/version-range match; the point is skipped (same tracer factory: a default-on match wins over a default-off one, otherwise the first wins; different factories: kept side by side when either is default-off, otherwise the first wins).
NRDG004 | NewRelic.OtelDistro.Generators | Warning | Instrumentation XML file is malformed; the file is skipped.
NRDG005 | NewRelic.OtelDistro.Generators | Error | The instrumentation definitions table cannot be written (a format limit or an empty required value); no source is generated.
NRDG006 | NewRelic.OtelDistro.Generators | Warning | An instrumentation minVersion/maxVersion is not a version the NR profiler accepts; it is treated as no bound, as in agent mode.
NRDG007 | NewRelic.OtelDistro.Generators | Warning | An instrumentation version bound has a non-zero revision, which OTel cannot express; it is widened to the enclosing three-part bound.
NRDG008 | NewRelic.OtelDistro.Generators | Warning | An instrumentation match's version range is empty after conversion; the point is skipped.
NRDG009 | NewRelic.OtelDistro.Generators | Warning | An overload-wide ("default-off" when only default-off points are involved) match is in range for versions where no explicit match of the same method and arity is; the distro instruments the overload there, agent mode instruments nothing.
NRDG010 | NewRelic.OtelDistro.Generators | Warning | Matches ("default-off" when all are default-off) with the same assembly/class/method/parameters have overlapping version ranges and different tracer factories; the distro uses XML order, agent mode's choice is unspecified.
NRDG011 | NewRelic.OtelDistro.Generators | Warning | NR covers a library only with default-off points (enabled="false"), so its OTel instrumentation is not disabled.
