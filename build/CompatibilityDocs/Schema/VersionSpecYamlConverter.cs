// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace CompatibilityDocs.Schema;

// Deserializes a minVersion/latestVersion YAML node that is either a scalar string or a
// mapping of tab -> version string into a VersionSpec. Serialization is unused.
public sealed class VersionSpecYamlConverter : IYamlTypeConverter
{
    public bool Accepts(Type type) => type == typeof(VersionSpec);

    public object? ReadYaml(IParser parser, Type type, ObjectDeserializer rootDeserializer)
    {
        if (parser.Current is Scalar scalar)
        {
            parser.MoveNext();
            return VersionSpec.Single(scalar.Value);
        }

        if (parser.Current is MappingStart)
        {
            parser.MoveNext();
            var map = new Dictionary<string, string>();
            while (parser.Current is not MappingEnd)
            {
                var key = ((Scalar)parser.Current!).Value;
                parser.MoveNext();
                var value = ((Scalar)parser.Current!).Value;
                parser.MoveNext();
                map[key] = value;
            }
            parser.MoveNext(); // consume MappingEnd
            return VersionSpec.Map(map);
        }

        var mark = parser.Current?.Start ?? Mark.Empty;
        throw new YamlException(mark, parser.Current?.End ?? Mark.Empty,
            "minVersion/latestVersion must be a string or a {core, framework} map.");
    }

    public void WriteYaml(IEmitter emitter, object? value, Type type, ObjectSerializer serializer) =>
        throw new NotSupportedException();
}
