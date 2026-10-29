// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Threading.Tasks;
using MassTransit;

namespace MultiFunctionApplicationHelpers.NetStandardLibraries.MassTransit;

public class FailingMessageConsumer : IConsumer<FailingMessage>
{
    public const string ExceptionMessagePrefix = "MassTransit consumer failure: ";

    public Task Consume(ConsumeContext<FailingMessage> context)
    {
        ConsoleMFLogger.Info($"Received failing message {context.Message.Text}, throwing");
        throw new InvalidOperationException(ExceptionMessagePrefix + context.Message.Text);
    }
}
