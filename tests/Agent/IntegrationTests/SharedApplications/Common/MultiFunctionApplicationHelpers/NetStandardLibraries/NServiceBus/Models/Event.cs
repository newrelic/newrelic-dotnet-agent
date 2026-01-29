// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#if !NET462

using NServiceBus;

namespace MultiFunctionApplicationHelpers.NetStandardLibraries.NServiceBus.Models;

public class Event : BaseModel, IEvent { }

#endif
