// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;

namespace CompatibilityDocs.Schema;

public class SchemaValidationException : Exception
{
    public SchemaValidationException(string message) : base(message) { }
}
