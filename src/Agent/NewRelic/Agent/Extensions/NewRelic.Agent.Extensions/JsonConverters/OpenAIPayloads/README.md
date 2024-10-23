## OpenAI (LLM) payloads in NewRelic.Core

These classes are used in the OpenAI instrumentation wrapper.  They are placed here in NewRelic.Agent.Extensions.JsonConverters,
rather than in the wrapper project, to avoid the wrapper having a dependency on Newtonsoft.Json.
NewRelic.Agent.Extensions already ILRepacks Newtonsoft.Json so it is available here for sure.
