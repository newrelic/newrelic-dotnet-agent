// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#if !NET462

using System;

namespace MultiFunctionApplicationHelpers.NetStandardLibraries.NServiceBus.Models;

public class BaseModel
{
    public Guid Id = Guid.NewGuid();
}
#endif
