// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using MassTransit;

namespace MassTransitTestApp;

/// <summary>
/// Marker interface for the Amazon SQS MultiBus instance.
/// MassTransit MultiBus requires a unique interface per additional bus.
/// </summary>
public interface ISqsBus : IBus
{
}
