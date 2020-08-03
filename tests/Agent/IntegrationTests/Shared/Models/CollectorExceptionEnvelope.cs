// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;

namespace NewRelic.Agent.IntegrationTests.Shared.Models
{
    public class CollectorExceptionEnvelope
    {
        public readonly string Exception;

        public CollectorExceptionEnvelope(Exception exception)
        {
            Exception = null;
        }
    }
}
