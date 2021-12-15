// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NServiceBus;

#if !NET462

namespace NsbTests
{
    public class Command : BaseModel, ICommand { }
}

#endif
