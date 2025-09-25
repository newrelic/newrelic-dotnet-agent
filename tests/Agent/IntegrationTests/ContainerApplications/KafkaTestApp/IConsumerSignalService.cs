// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Threading.Tasks;

namespace KafkaTestApp
{
    public enum ConsumptionMode
    {
        Timeout,
        CancellationToken
    }

    public interface IConsumerSignalService
    {
        Task RequestConsumeAsync(ConsumptionMode mode);
    }
}
