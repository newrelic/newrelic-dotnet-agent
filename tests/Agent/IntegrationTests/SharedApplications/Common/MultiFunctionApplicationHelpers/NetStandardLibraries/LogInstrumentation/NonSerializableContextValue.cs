// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace MultiFunctionApplicationHelpers.NetStandardLibraries.LogInstrumentation;

// A context-data value whose object graph cannot be serialized by Newtonsoft.Json. The self
// reference triggers "Self referencing loop detected" under default serializer settings,
// the same way the property graph of a real ASP.NET Core Endpoint does. Its ToString()
// contains spaces and parentheses, so if the agent ever wrote it unquoted the resulting
// log_event_data payload would be unambiguously invalid JSON.
public class NonSerializableContextValue
{
    public const string ToStringValue = "Sample.App.Controllers.WidgetController.IsEnabled (Sample.App)";

    public NonSerializableContextValue Self { get; }

    public NonSerializableContextValue()
    {
        Self = this;
    }

    public override string ToString() => ToStringValue;
}
