// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Threading;
using System.Threading.Tasks;

namespace NewRelic.Agent.Core.DataTransport;

public interface IDelayer
{
    void Delay(int milliseconds, CancellationToken token);
}

public class Delayer : IDelayer
{
    public void Delay(int milliseconds, CancellationToken token)
    {
        try
        {
            Task.Delay(milliseconds).Wait(token);
        }
        catch (OperationCanceledException)
        {
            //A cancelation was triggered and we don't need the Delayer to bubble up the exception
        }
    }
}