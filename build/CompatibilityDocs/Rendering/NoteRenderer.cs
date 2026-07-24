// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using CompatibilityDocs.Schema;

namespace CompatibilityDocs.Rendering;

public class NoteRenderer
{
    public virtual string Render(Note note)
    {
        switch (note.Type)
        {
            case "addedInAgent":
                return $"Versions {note.SinceVersion}+ supported since agent v{note.AgentVersion}.";
            case "maxSupportedVersion":
                return $"Maximum supported version: {note.Version}.";
            case "knownIncompatibleVersions":
                var body = note.Versions is { Count: > 0 } ? string.Join(", ", note.Versions) : note.Text;
                return $"Known incompatible versions: {body}.";
            case "requiresHybridAgent":
                const string otelLink = "[OpenTelemetry API support](https://docs.newrelic.com/docs/apm/agents/manage-apm-agents/opentelemetry-api-support/)";
                return string.IsNullOrEmpty(note.AboveVersion)
                    ? $"Supported only when {otelLink} is enabled."
                    : $"Versions later than {note.AboveVersion} are supported only when {otelLink} is enabled.";
            case "freeform":
                return note.Text ?? "";
            default:
                throw new ArgumentException($"Unknown note type '{note.Type}'.");
        }
    }
}
