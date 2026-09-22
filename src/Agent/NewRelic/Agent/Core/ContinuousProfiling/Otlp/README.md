# Vendored OTLP proto files

`common.proto`, `profiles.proto`, `profiles_service.proto`, and `resource.proto`
in this directory are vendored from
[open-telemetry/opentelemetry-proto](https://github.com/open-telemetry/opentelemetry-proto)
at commit `bf321114b3d708cc4f19ba801732b5e8c61fc58e` (profiles v1development,
alpha). They define the OTLP wire format used to export continuous profiling
data and are compiled at build time via `Google.Protobuf`/`Grpc.Tools` codegen
into `NewRelic.Agent.Core.dll`.

Licensed under the Apache License, Version 2.0. See the license header in each
file and the corresponding entry in
[`licenses/THIRD_PARTY_NOTICES.txt`](../../../../../../../licenses/THIRD_PARTY_NOTICES.txt)
at the repo root.

Do not hand-edit these files beyond what's needed to scope codegen to this
agent (e.g. removing unused service definitions) — re-vendor from upstream at
a newer commit instead, and update the pinned commit hash here, in each file's
header comment, and in `THIRD_PARTY_NOTICES.txt`.
