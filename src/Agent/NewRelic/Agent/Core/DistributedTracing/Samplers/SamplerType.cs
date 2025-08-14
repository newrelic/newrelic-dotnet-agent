// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.Agent.Core.DistributedTracing.Samplers
{
    public enum SamplerType
    {
        Root,
        RemoteParentSampled,
        RemoteParentNotSampled,
    }
}
