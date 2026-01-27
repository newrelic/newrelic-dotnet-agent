// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;

namespace NewRelic.Agent.Core.Utilities;

/// <summary>
/// A custom attribute used to indicate that this method/class is covered by
/// integration tests and can be excluded from unit test code coverage
/// analysis.
/// </summary>
public class NrCoveredByIntegrationTestsAttribute : Attribute
{
}