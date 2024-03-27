## AWS Bedrock (LLM) payloads in NewRelic.Core

These classes are used in the Bedrock instrumentation wrapper.  They are placed here in NewRelic.Core.JsonConverters,
rather than in the wrapper project, to avoid the wrapper having a dependency on Newtonsoft.Json.
NewRelic.Core already ILRepacks Newtonsoft.Json so it is available here for sure.

