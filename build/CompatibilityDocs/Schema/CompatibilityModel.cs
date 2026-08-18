// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;

namespace CompatibilityDocs.Schema;

public class CompatibilityModel
{
    public List<Category> Categories { get; set; } = new();
}
