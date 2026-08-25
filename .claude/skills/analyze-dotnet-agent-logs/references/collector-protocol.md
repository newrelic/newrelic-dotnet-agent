# Collector protocol as it appears in the log

Field names and log lines below come from agent source. Sample lines are
synthetic, built to match the verified format.

## How one call looks

Every call is keyed by a request guid, so a request and its response can be
paired even when other threads interleave between them.

| Line | Level | Source |
|---|---|---|
| `Request({guid}): Invoking "{method}"` | FINEST | `HttpCollectorWire.cs:48` |
| `Request({guid}): Invoked "{method}" with : {payload}` | DEBUG | `HttpCollectorWire.cs:84` |
| `Request({guid}): Invocation of "{method}" yielded response : {response}` | DEBUG | `HttpCollectorWire.cs:85` |
| `Request({guid}): Invocation of "{method}" returned response headers : {headers}` | DEBUG | `HttpCollectorWire.cs:73,87` |
| `Request({guid}): Received a {code} {status} response invoking method "{method}" with payload "{payload}"` | DEBUG | `ConnectionHandler.cs:361` |
| `Request({guid}): An error occurred invoking method "{method}" with payload "{payload}": {exception}` | DEBUG | `ConnectionHandler.cs:372` |
| `Request({guid}): Dropped large payload: size: {n}, max_payload_size_bytes={m}` | ERROR | `HttpCollectorWire.cs:96` |

At INFO none of these appear. A log with no `Invoked` lines is not a log with no
traffic; it is a log at the wrong level.

The request URI never appears in these lines. It carries the license key and the
`run_id`, and it shows up only inside exception text.

## Endpoints

| Method | Purpose |
|---|---|
| `preconnect` | Ask which collector host to use for this license key |
| `connect` | Register the run, send settings and environment, get server config |
| `agent_settings` | Report the settings the agent ended up with, after server config |
| `metric_data` | Timeslice metrics, every harvest |
| `analytic_event_data` | Transaction events |
| `span_event_data` | Span events |
| `error_event_data` | Error events |
| `custom_event_data` | Custom events |
| `log_event_data` | Forwarded application logs |
| `transaction_sample_data` | Transaction traces |
| `sql_trace_data` | Slow SQL traces |
| `error_data` | Error traces |
| `get_agent_commands` | Poll for commands, for example a thread profile request |
| `agent_command_results` | Report command outcomes |
| `profile_data` | Thread profile results |
| `update_loaded_modules` | Assembly inventory |
| `shutdown` | End the run cleanly |

Healthy sequence: `preconnect`, `connect`, `agent_settings`, then repeating
harvests of `metric_data` plus whichever data types have samples, with
`get_agent_commands` polling alongside, and `shutdown` at the end. A run that
stops after `preconnect` or `connect` never reported anything.

## connect request

Top-level fields (`ConnectModel.cs`): `pid`, `language`, `host`,
`display_host`, `app_name`, `agent_version`, `agent_version_timestamp`,
`security_settings`, `high_security`, `event_harvest_config`, `identifier`,
`labels`, `settings`, `metadata`, `utilization`, `environment`.

The ones that answer support questions:

- `app_name` - where the data will land. Compare against what the customer
  expects to see in the UI.
- `host` and `display_host` - the entity name the customer will look for.
- `settings` - the full effective configuration, one flat key per setting
  (`agent.*`). This is the fastest way to see what the agent actually resolved,
  including `agent.license_key.configured`, which is a boolean. The key itself
  is never in here.
- `high_security` - when true, request parameters are dropped and SQL is
  obfuscated regardless of local config.
- `event_harvest_config.harvest_limits` - the per-type reservoir sizes the agent
  asked for: `analytic_event_data`, `custom_event_data`, `error_event_data`,
  `span_event_data`, `log_event_data`.
- `utilization` - cloud and container detection. Wrong entity grouping usually
  starts here.

## connect response

Fields the agent reads (`ServerConfiguration.cs`). `agent_run_id` is required;
everything else is optional and absent means "keep the local value".

| Field | Effect |
|---|---|
| `agent_run_id` | Identifies the run. A new one after a restart means the agent reconnected. |
| `collect_errors`, `collect_traces`, `collect_analytics_events`, `collect_error_events`, `collect_span_events`, `collect_custom_events`, `collect_ai` | Server-side kill switches. A `false` here explains missing data of exactly that type. |
| `agent_config` | Server-side configuration, which overrides local config unless the agent is set to ignore it. |
| `event_harvest_config`, `span_event_harvest_config` | Faster harvest cycles and reservoir sizes the server assigned. |
| `data_report_period` | Harvest interval. |
| `max_payload_size_in_bytes` | The ceiling behind `Dropped large payload`. |
| `high_security` | Server-side high security. |
| `sampling_target`, `sampling_target_period_in_seconds` | Span sampling budget. |
| `entity_guid`, `account_id`, `primary_application_id`, `trusted_account_key`, `cross_process_id` | Identity and distributed-tracing trust. |
| `messages` | Server messages, which the agent logs at their stated level. |
| `metric_name_rules`, `transaction_name_rules`, `url_rules` | Renaming rules. Unexpected transaction names often come from here, not from the agent. |
| `request_headers_map` | Headers the agent must attach to later requests. |

## Response status handling

`DataTransportService.cs` maps the HTTP status to an action:

| Status | Action |
|---|---|
| 400, 401, 403, 404, 405, 407, 409, 410, 411, 414, 415, 417, 431 | Discard the payload |
| 408, 429, 500, 503 | Retain and retry on the next harvest |
| 413 | Reduce size if possible, otherwise discard |
| anything else | Discard |

Two statuses also change the agent's life cycle:

- 401 or 409 triggers a restart, which produces a fresh `preconnect` and
  `connect` inside the same session.
- 410 triggers shutdown, logged as `Shutting down: {message}` at INFO, preceded
  by `The server has requested that the agent disconnect. The agent is shutting
  down.`

So a session that keeps reconnecting points at 401 or 409, and a session that
ends without a customer-initiated stop points at 410.

## Positional payloads

Several payloads serialize as positional JSON arrays, not objects, so a field
key is the only way to read them.

`transaction_sample_data` trace, per trace
(`TransactionTraceWireModel.cs`):

| Index | Field |
|---|---|
| 0 | start time, Unix milliseconds |
| 1 | duration, milliseconds |
| 2 | transaction metric name |
| 3 | request URI |
| 4 | trace data (the nested segment tree) |
| 5 | transaction guid |
| 6 | unused, always null |
| 7 | unused, always false |
| 8 | xray session id, never set by the .NET agent |
| 9 | synthetics resource id |

`metric_data` metric values (`MetricDataWireModel.cs`):

| Index | Field |
|---|---|
| 0 | call count |
| 1 | total time, seconds |
| 2 | exclusive time, seconds |
| 3 | minimum time, seconds |
| 4 | maximum time, seconds |
| 5 | sum of squares |

Indexes 3 and 4: the doc comments on the fields name them the other way round,
but `BuildAggregateData` folds index 3 with `Math.Min` from a `float.MaxValue`
seed and index 4 with `Math.Max` from a `float.MinValue` seed. The aggregation
decides the semantics, so index 3 is the minimum. Treat the field comments as
wrong.

Nothing in a .NET agent payload is gzip-plus-base64. Other agents compress the
trace tree into an encoded string; this one sends plain nested arrays.

## Base64 in the log

Distributed-trace payloads are base64-encoded JSON, and they appear in wrapper
and distributed-tracing lines rather than in collector payloads. `nrlog.py
decode` turns one into JSON. Legacy cross-application-tracing headers are also
base64 but are XOR-obfuscated with the `encoding_key` from the connect
response, so decoding one needs that key.
