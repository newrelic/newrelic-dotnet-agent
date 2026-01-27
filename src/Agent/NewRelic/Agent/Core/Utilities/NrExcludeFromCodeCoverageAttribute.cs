// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;

namespace NewRelic.Agent.Core.Utilities;

/// <summary>
/// A custom attribute used to exclude methods and/or classes from code coverage.
/// Lets us avoid a dependency on System.Diagnostics.CodeAnalysis
/// </summary>
public class NrExcludeFromCodeCoverageAttribute : Attribute
{
}