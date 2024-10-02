// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;

namespace NewRelic.Agent.Core.DistributedTracing
{
    public class DistributedTraceException : Exception
    {
        public DistributedTraceException()
        {
        }

        public DistributedTraceException(string message) : base(message)
        {
        }

        public DistributedTraceException(string message, Exception inner) : base(message, inner)
        {
        }
    }

    public class DistributedTraceAcceptPayloadException : DistributedTraceException
    {
        public DistributedTraceAcceptPayloadException()
        {
        }

        public DistributedTraceAcceptPayloadException(string message) : base(message)
        {
        }

        public DistributedTraceAcceptPayloadException(string message, Exception inner) : base(message, inner)
        {
        }
    }

    public class DistributedTraceAcceptPayloadParseException : DistributedTraceAcceptPayloadException
    {
        public DistributedTraceAcceptPayloadParseException()
        {
        }

        public DistributedTraceAcceptPayloadParseException(string message) : base(message)
        {
        }

        public DistributedTraceAcceptPayloadParseException(string message, Exception inner) : base(message, inner)
        {
        }
    }

    public class DistributedTraceAcceptPayloadNullException : DistributedTraceAcceptPayloadException
    {
        public DistributedTraceAcceptPayloadNullException()
        {
        }

        public DistributedTraceAcceptPayloadNullException(string message) : base(message)
        {
        }

        public DistributedTraceAcceptPayloadNullException(string message, Exception inner) : base(message, inner)
        {
        }
    }

    public class DistributedTraceAcceptPayloadVersionException : DistributedTraceAcceptPayloadException
    {
        public DistributedTraceAcceptPayloadVersionException()
        {
        }

        public DistributedTraceAcceptPayloadVersionException(string message) : base(message)
        {
        }

        public DistributedTraceAcceptPayloadVersionException(string message, Exception inner) : base(message, inner)
        {
        }
    }

}
