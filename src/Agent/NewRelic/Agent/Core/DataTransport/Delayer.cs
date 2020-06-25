/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
using System.Threading;
using System.Threading.Tasks;

namespace NewRelic.Agent.Core.DataTransport
{
    public interface IDelayer
    {
        void Delay(int milliseconds, CancellationToken token);
    }

    public class Delayer : IDelayer
    {
        public void Delay(int milliseconds, CancellationToken token)
        {
            Task.Delay(milliseconds).Wait(token);
        }
    }

}
