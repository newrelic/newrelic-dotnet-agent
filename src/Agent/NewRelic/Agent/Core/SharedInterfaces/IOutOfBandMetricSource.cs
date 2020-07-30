﻿/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
using NewRelic.Agent.Core.WireModels;

namespace NewRelic.Agent.Core.SharedInterfaces
{
    public delegate void PublishMetricDelegate(MetricWireModel metric);

    public interface IOutOfBandMetricSource
    {
        void RegisterPublishMetricHandler(PublishMetricDelegate publishMetricDelegate);
    }
}
