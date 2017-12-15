## Release Notes

### Current
* Transactions now start immediately prior to the first middleware in the pipeline with the root segment being named "Middleware Pipeline." No longer instruments `ProcessRequestAsync`. 

### Previous

#### 6.19.29.0-beta
* Fixed bug where certain types of middleware in the pipeline would result in dropped instrumentation. In some cases, this would also result in [metric grouping issues](https://docs.newrelic.com/docs/agents/manage-apm-agents/troubleshooting/metric-grouping-issues) (MGIs) due to improper transaction naming. These issues would commonly occur when authentication or CORS were added to an application. Such as calls like: `services.AddAuthorization()` and `app.UseCors("CorsPolicy")`.

#### 6.18.117.0-beta
* Added support for setting `NewRelic.AppName` and related settings in `appSettings.json`. For related configuration, see: [app-config-settings](https://docs.newrelic.com/docs/agents/net-agent/configuration/net-agent-configuration#app-config-settings).
* Application specific `newrelic.config` will now be used whether the application was published or not.
* Fixed issue where agent would intermittently fail to startup with `[Error] 2017-10-16 19:39:10 Unable to open file. File path: ..config`.
* Fixed issue where the agent would crash for older CPU architectures on Linux, resulting in an "illegal instruction" error.
* Added additional default transaction naming to prevent [metric grouping issues](https://docs.newrelic.com/docs/agents/manage-apm-agents/troubleshooting/metric-grouping-issues) (MGIs). Typically, MVC/WebApi applications are not prone to MGIs because transactions are named using the pattern: `Controller/Action/{ParameterName 1}/.../{ParameterName n}`. Requests that throw exceptions, or return prior to MVC routing, would previously be named using the path which could result in MGIs. Transactions that throw exceptions or have status codes in the 400-500+ range, that have not hit MVC routing, will now have a default naming of `/StatusCode/<code>`. For example: `/StatusCode/403`. Traces will still display the url for debugging issues.
* Added Docker instructions for Windows and Linux.
* Various readme documentation updates.