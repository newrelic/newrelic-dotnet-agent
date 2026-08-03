// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;

namespace ArtifactBuilder;

public class PackagingException : Exception
{
    public PackagingException(string message) : base(message) { }
}