# OpenTelemetry Proto Files

These `.proto` files are sourced from the OpenTelemetry Protocol repository, version v1.9.0.

Attribution:
- Repository: https://github.com/open-telemetry/opentelemetry-proto
- Version tag: v1.9.0
- Direct paths:
  - Collector metrics service: https://github.com/open-telemetry/opentelemetry-proto/tree/v1.9.0/opentelemetry/proto/collector/metrics/v1
  - Metrics types: https://github.com/open-telemetry/opentelemetry-proto/tree/v1.9.0/opentelemetry/proto/metrics/v1
  - Common types: https://github.com/open-telemetry/opentelemetry-proto/tree/v1.9.0/opentelemetry/proto/common/v1
  - Resource types: https://github.com/open-telemetry/opentelemetry-proto/tree/v1.9.0/opentelemetry/proto/resource/v1

These files are used to generate C# classes via `Grpc.Tools` for parsing OTLP metrics payloads over HTTP in the `MockNewRelic` application.
