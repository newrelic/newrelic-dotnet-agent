| Environment Variable | Default/Value (if determinable) | Purpose |
|---|---|---|
| APP_POOL_ID | N/A | IIS application pool name used as a fallback for application naming. |
| ASPNETCORE_IIS_APP_POOL_ID | N/A | ASP.NET Core IIS app pool name used as a fallback for application naming. |
| AWS_LAMBDA_FUNCTION_NAME | N/A | Enables serverless mode if present; used for naming in Lambda. |
| AWS_LAMBDA_FUNCTION_VERSION | N/A | Lambda function version (reported in serverless mode). |
| FUNCTIONS_WORKER_RUNTIME | N/A | Detection flag for Azure Functions mode (presence-only). |
| IISEXPRESS_SITENAME | N/A | Used for application naming when running under IIS Express. |
| NEWRELIC_ACCOUNT_ID | N/A | Overrides distributed tracing account ID in serverless mode. |
| NEWRELIC_AGENT_CONTROL_ENABLED | false | Enables agent control features (health reporting, remote commands). |
| NEWRELIC_AGENT_CONTROL_HEALTH_DELIVERY_LOCATION | N/A | Destination for agent control health reports. |
| NEWRELIC_AGENT_CONTROL_HEALTH_FREQUENCY | 5 (seconds) | Frequency for agent control health reporting. |
| NEWRELIC_APDEX_T | 0.5 (seconds, serverless only) | Overrides Apdex T in serverless mode for transaction trace threshold. |
| NEWRELIC_APPLICATION_LOGGING_ENABLED | true | Enables application logging features (forwarding/metrics/decorating gate). |
| NEWRELIC_APPLICATION_LOGGING_FORWARDING_CONTEXT_DATA_ENABLED | false | Enables forwarding of selected context fields with log events. |
| NEWRELIC_APPLICATION_LOGGING_FORWARDING_CONTEXT_DATA_EXCLUDE | SpanId,TraceId,ParentId | Exclude list for forwarded log context fields. |
| NEWRELIC_APPLICATION_LOGGING_FORWARDING_CONTEXT_DATA_INCLUDE | "" | Include list for forwarded log context fields. |
| NEWRELIC_APPLICATION_LOGGING_FORWARDING_ENABLED | true | Enables log event forwarding. |
| NEWRELIC_APPLICATION_LOGGING_FORWARDING_LABELS_ENABLED | false | Enables forwarding of labels with log events. |
| NEWRELIC_APPLICATION_LOGGING_FORWARDING_LABELS_EXCLUDE | "" | Exclude list of labels to omit from forwarded logs. |
| NEWRELIC_APPLICATION_LOGGING_FORWARDING_LOG_LEVEL_DENYLIST | N/A | List of log levels to drop during forwarding (e.g., DEBUG). |
| NEWRELIC_APPLICATION_LOGGING_FORWARDING_MAX_SAMPLES_STORED | 10000 (or server override) | Max number of log events stored per harvest. |
| NEWRELIC_APPLICATION_LOGGING_LOCAL_DECORATING_ENABLED | false | Enables local log decoration with NR attributes. |
| NEWRELIC_LICENSEKEY | N/A | Alias for NEW_RELIC_LICENSE_KEY. |
| NEWRELIC_LOG_DIRECTORY | N/A | Alias for NEW_RELIC_LOG_DIRECTORY. |
| NEWRELIC_LOG_LEVEL | "info" | Alias for NEW_RELIC_LOG_LEVEL. |
| NEW_RELIC_ALLOW_ALL_HEADERS | N/A | Allows all request headers (subject to HSM restrictions). |
| NEW_RELIC_APP_NAME | N/A | Explicit application name(s), comma-separated. |
| NEW_RELIC_APDEX_T | 0.5 (seconds, serverless only) | Same as NEWRELIC_APDEX_T (serverless only). |
| NEW_RELIC_APPLICATION_LOGGING_ENABLED | true | Same as NEWRELIC_APPLICATION_LOGGING_ENABLED. |
| NEW_RELIC_APPLICATION_LOGGING_FORWARDING_CONTEXT_DATA_ENABLED | false | Same as NEWRELIC_APPLICATION_LOGGING_FORWARDING_CONTEXT_DATA_ENABLED. |
| NEW_RELIC_APPLICATION_LOGGING_FORWARDING_CONTEXT_DATA_EXCLUDE | SpanId,TraceId,ParentId | Same as NEWRELIC_APPLICATION_LOGGING_FORWARDING_CONTEXT_DATA_EXCLUDE. |
| NEW_RELIC_APPLICATION_LOGGING_FORWARDING_CONTEXT_DATA_INCLUDE | "" | Same as NEWRELIC_APPLICATION_LOGGING_FORWARDING_CONTEXT_DATA_INCLUDE. |
| NEW_RELIC_APPLICATION_LOGGING_FORWARDING_ENABLED | true | Same as NEWRELIC_APPLICATION_LOGGING_FORWARDING_ENABLED. |
| NEW_RELIC_APPLICATION_LOGGING_FORWARDING_LABELS_ENABLED | false | Same as NEWRELIC_APPLICATION_LOGGING_FORWARDING_LABELS_ENABLED. |
| NEW_RELIC_APPLICATION_LOGGING_FORWARDING_LABELS_EXCLUDE | "" | Same as NEWRELIC_APPLICATION_LOGGING_FORWARDING_LABELS_EXCLUDE. |
| NEW_RELIC_APPLICATION_LOGGING_FORWARDING_LOG_LEVEL_DENYLIST | N/A | Same as NEWRELIC_APPLICATION_LOGGING_FORWARDING_LOG_LEVEL_DENYLIST. |
| NEW_RELIC_APPLICATION_LOGGING_FORWARDING_MAX_SAMPLES_STORED | 10000 (or server override) | Same as NEWRELIC_APPLICATION_LOGGING_FORWARDING_MAX_SAMPLES_STORED. |
| NEW_RELIC_APPLICATION_LOGGING_LOCAL_DECORATING_ENABLED | false | Same as NEWRELIC_APPLICATION_LOGGING_LOCAL_DECORATING_ENABLED. |
| NEW_RELIC_AZURE_FUNCTION_MODE_ENABLED | true | Enables special Azure Functions agent behavior. |
| NEW_RELIC_BROWSER_MONITORING_AUTO_INSTRUMENT | true | Enables automatic browser monitoring instrumentation. |
| NEW_RELIC_CLOUD_AWS_ACCOUNT_ID | N/A | Overrides AWS account ID for cloud metadata reporting. |
| NEW_RELIC_CODE_LEVEL_METRICS_ENABLED | true | Enables code-level metrics collection. |
| NEW_RELIC_CONFIG_OBSCURING_KEY | N/A | Obscuring key used to decode obfuscated secrets (proxy password). |
| NEW_RELIC_DISTRIBUTED_TRACING_ENABLED | local config | Enables distributed tracing. |
| NEW_RELIC_DISTRIBUTED_TRACING_EXCLUDE_NEWRELIC_HEADER | false | Excludes the New Relic DT header. |
| NEW_RELIC_DISTRIBUTED_TRACING_SAMPLER_REMOTE_PARENT_NOT_SAMPLED | N/A | Overrides remote parent not-sampled sampler type. |
| NEW_RELIC_DISTRIBUTED_TRACING_SAMPLER_REMOTE_PARENT_NOT_SAMPLED_TRACE_ID_RATIO_BASED_RATIO | N/A | Ratio for remote parent not-sampled trace-id-ratio-based sampler. |
| NEW_RELIC_DISTRIBUTED_TRACING_SAMPLER_REMOTE_PARENT_SAMPLED | N/A | Overrides remote parent sampled sampler type. |
| NEW_RELIC_DISTRIBUTED_TRACING_SAMPLER_REMOTE_PARENT_SAMPLED_TRACE_ID_RATIO_BASED_RATIO | N/A | Ratio for remote parent sampled trace-id-ratio-based sampler. |
| NEW_RELIC_DISTRIBUTED_TRACING_SAMPLER_ROOT | N/A | Overrides root sampler type. |
| NEW_RELIC_DISTRIBUTED_TRACING_SAMPLER_ROOT_TRACE_ID_RATIO_BASED_RATIO | N/A | Ratio for root trace-id-ratio-based sampler. |
| NEW_RELIC_DISABLE_APPDOMAIN_CACHING | false | Disables AppDomain-level caching logic used by the agent. |
| NEW_RELIC_DISABLE_FILE_SYSTEM_WATCHER | false (true in serverless or when logging disabled) | Disables file system watcher used for instrumentation config reloads. |
| NEW_RELIC_DISABLE_SAMPLERS | false | Disables host-level samplers (e.g., GC/memory sampling). |
| NEW_RELIC_ERROR_COLLECTOR_CAPTURE_EVENTS | true | Enables error event capture (unless max samples = 0). |
| NEW_RELIC_ERROR_COLLECTOR_EXPECTED_ERROR_CODES | N/A | Expected HTTP status codes or ranges (comma/space-delimited). |
| NEW_RELIC_ERROR_COLLECTOR_IGNORE_ERROR_CODES | N/A | HTTP status codes to ignore (comma/space-delimited). |
| NEW_RELIC_FORCE_NEW_TRANSACTION_ON_NEW_THREAD | false | Forces new transaction when work moves to a new thread. |
| NEW_RELIC_GC_SAMPLER_V2_ENABLED | false | Enables GC sampler v2 (bootstrap read; OR’d with local config). |
| NEW_RELIC_HIGH_SECURITY | false | Enables High Security Mode; restricts sensitive data collection. |
| NEW_RELIC_HOST | N/A | Collector host override. |
| NEW_RELIC_HYBRID_HTTP_CONTEXT_STORAGE_ENABLED | false | Enables hybrid HTTP context storage for mixed hosting scenarios. |
| NEW_RELIC_IGNORE_SERVER_SIDE_CONFIG | false | Ignores server-side configuration settings. |
| NEW_RELIC_INFINITE_TRACING_COMPRESSION | true | Enables compression for infinite tracing gRPC. |
| NEW_RELIC_INFINITE_TRACING_EXIT_TIMEOUT | 5000 (ms) | Timeout for agent shutdown when infinite tracing is enabled. |
| NEW_RELIC_INFINITE_TRACING_SPAN_EVENTS_BATCH_SIZE | 700 | Batch size for span events producer. |
| NEW_RELIC_INFINITE_TRACING_SPAN_EVENTS_PARTITION_COUNT | 62 | Partition count for span events processing. |
| NEW_RELIC_INFINITE_TRACING_SPAN_EVENTS_QUEUE_SIZE | 100000 | Queue size for span events buffering. |
| NEW_RELIC_INFINITE_TRACING_SPAN_EVENTS_STREAMS_COUNT | 10 | Number of span event consumer streams/workers. |
| NEW_RELIC_INFINITE_TRACING_SPAN_EVENTS_TEST_DELAY | N/A | Test-only artificial delay (ms) for span events. |
| NEW_RELIC_INFINITE_TRACING_SPAN_EVENTS_TEST_FLAKY | N/A | Test-only flaky behavior ratio for span event delivery. |
| NEW_RELIC_INFINITE_TRACING_SPAN_EVENTS_TEST_FLAKY_CODE | N/A | Test-only status code for flaky simulation. |
| NEW_RELIC_INFINITE_TRACING_TIMEOUT_CONNECT | 10000 (ms) | Connection timeout for infinite tracing. |
| NEW_RELIC_INFINITE_TRACING_TIMEOUT_SEND | 10000 (ms) | Send timeout for infinite tracing. |
| NEW_RELIC_INFINITE_TRACING_TRACE_OBSERVER_HOST | N/A | Host for infinite tracing trace observer (gRPC endpoint). |
| NEW_RELIC_INFINITE_TRACING_TRACE_OBSERVER_PORT | 443 | Port for infinite tracing observer. |
| NEW_RELIC_INFINITE_TRACING_TRACE_OBSERVER_SSL | N/A | SSL setting for infinite tracing observer ("true"/"false"). |
| NEW_RELIC_LABELS | N/A | Application labels (comma/space-delimited). |
| NEW_RELIC_LICENSE_KEY | N/A | Primary source for license key (alias: NEWRELIC_LICENSEKEY). |
| NEW_RELIC_LOG | N/A | Log file name override. |
| NEW_RELIC_LOG_CONSOLE | false | Enables logging to console. |
| NEW_RELIC_LOG_DIRECTORY | N/A | Log directory override (alias: NEWRELIC_LOG_DIRECTORY). |
| NEW_RELIC_LOG_ENABLED | true | Enables agent logging. |
| NEW_RELIC_LOG_LEVEL | "info" | Agent logging level (alias: NEWRELIC_LOG_LEVEL). |
| NEW_RELIC_LOG_MAX_FILES | 4 | Max number of log files retained. |
| NEW_RELIC_LOG_MAX_FILE_SIZE_MB | 50 | Max size per log file in MB. |
| NEW_RELIC_LOG_ROLLING_STRATEGY | "size" | Log rolling strategy ("size" or "day"). |
| NEW_RELIC_OPEN_TELEMETRY_BRIDGE_ENABLED | false | Globally enables OpenTelemetry bridge. |
| NEW_RELIC_OPEN_TELEMETRY_BRIDGE_TRACING_ENABLED | true (if bridge enabled) | Enables tracing portion of the OpenTelemetry bridge. |
| NEW_RELIC_PORT | 443 | Collector port override. |
| NEW_RELIC_PRIMARY_APPLICATION_ID | N/A | Overrides primary application ID in serverless mode. |
| NEW_RELIC_PROCESS_HOST_DISPLAY_NAME | N/A | Overrides process host display name. |
| NEW_RELIC_PROXY_DOMAIN | N/A | Proxy domain for NTLM/AD scenarios. |
| NEW_RELIC_PROXY_HOST | N/A | Proxy host configuration. |
| NEW_RELIC_PROXY_PASS | N/A | Proxy password (plain). |
| NEW_RELIC_PROXY_PASS_OBFUSCATED | N/A | Proxy password (base64-obfuscated), decoded using obscuring key. |
| NEW_RELIC_PROXY_PORT | 8080 | Proxy port configuration. |
| NEW_RELIC_PROXY_URI_PATH | N/A | Proxy URI path. |
| NEW_RELIC_PROXY_USER | N/A | Proxy username. |
| NEW_RELIC_SECURITY_POLICIES_TOKEN | N/A | Enables/configures security policies mode by token presence/value. |
| NEW_RELIC_SEND_DATA_ON_EXIT | false | Enables sending data on process exit. |
| NEW_RELIC_SEND_DATA_ON_EXIT_THRESHOLD_MS | 60000 (ms) | Exit-time data send threshold in ms. |
| NEW_RELIC_SERVERLESS_MODE_ENABLED | local config | Explicitly enables/disables serverless mode. |
| NEW_RELIC_SPAN_EVENTS_MAX_SAMPLES_STORED | 2000 (or server override) | Max span events stored per harvest. |
| NEW_RELIC_TRUSTED_ACCOUNT_KEY | N/A | Overrides trusted account key in serverless mode. |
| NEW_RELIC_UTILIZATION_BILLING_HOSTNAME | N/A | Billing host override for utilization payloads. |
| NEW_RELIC_UTILIZATION_DETECT_AWS | true | Enables AWS environment detection. |
| NEW_RELIC_UTILIZATION_DETECT_AZURE | true | Enables Azure environment detection. |
| NEW_RELIC_UTILIZATION_DETECT_AZURE_APPSERVICE | true | Enables Azure App Service detection. |
| NEW_RELIC_UTILIZATION_DETECT_AZURE_FUNCTION | true | Enables Azure Function detection. |
| NEW_RELIC_UTILIZATION_DETECT_DOCKER | true | Enables Docker environment detection. |
| NEW_RELIC_UTILIZATION_DETECT_GCP | true | Enables GCP environment detection. |
| NEW_RELIC_UTILIZATION_DETECT_KUBERNETES | true | Enables Kubernetes environment detection. |
| NEW_RELIC_UTILIZATION_DETECT_PCF | true | Enables PCF environment detection. |
| NEW_RELIC_UTILIZATION_LOGICAL_PROCESSORS | N/A | Overrides logical processor count in utilization payload. |
| NEW_RELIC_UTILIZATION_TOTAL_RAM_MIB | N/A | Overrides total RAM (MiB) reported in utilization payload. |
| REGION_NAME | N/A | Azure Functions region used in Azure resource metadata. |
| RoleName | N/A | Application naming fallback in certain hosted environments. |
| WEBSITE_OWNER_NAME | N/A | Azure subscription/resource metadata (Linux Functions) used in resource ID. |
| WEBSITE_RESOURCE_GROUP | N/A | Azure resource group name (Windows Functions) used in resource ID. |
| WEBSITE_SITE_NAME | N/A | Azure Functions site name used for application naming. |
