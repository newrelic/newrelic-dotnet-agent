## Introduction

This library is used by the ConsoleScanner to analyze and compare instrumentation XML files against assemblies.  

## Some thoughts

[DONEish] We need to create a model that contains more than one assembly to work with instrumentation that has more than one, examples:
NewRelic.Providers.Wrapper.Asp35.Instrumentation.xml
NewRelic.Providers.Wrapper.Couchbase.Instrumentation.xml
NewRelic.Providers.Wrapper.AspNetCore.Instrumentation.xml
NewRelic.Providers.Wrapper.WebServices.Instrumentation.xml
NewRelic.Providers.Wrapper.Wcf3.Instrumentation.xml
NewRelic.Providers.Wrapper.StackExchangeRedis.Instrumentation.xml
NewRelic.Providers.Wrapper.Sql.Instrumentation.xml
NewRelic.Providers.Wrapper.Misc.Instrumentation.xml
NewRelic.Providers.Wrapper.MongoDb26.Instrumentation.xml

InstrumentationReport
	Assemblyreport
		List MatchReports
			List ExactMMreport 

	
If we had a wiki
- one section for each framework
- each section has one page per agent version
- Each <schedule> in the tool would either [create new agent-version page OR update existing one] with results, adding new framework versions as needed.
https://github.com/jaffinito/newrelic-dotnet-agent/wiki/tables-are-cool-I-guess
 
clone github wiki
make changes
push back up
uses pre-provide GH token
runs in GHA
- Workflow has different jobs for each framework that know how to pull down the packages
https://github.com/ikatyang/emoji-cheat-sheet#symbols
