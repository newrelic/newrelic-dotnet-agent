// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.Serialization;

namespace BenchmarkingTests.Scaffolding.Benchmarker
{
    public class BenchmarkerException : Exception
    {
        public BenchmarkerException()
        {
        }

        public BenchmarkerException(string message) : base(message)
        {
        }

        public BenchmarkerException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected BenchmarkerException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }


    public class BenchmarkerClassNotPublicException : BenchmarkerException
    {
        public BenchmarkerClassNotPublicException()
        {
        }

        public BenchmarkerClassNotPublicException(string message) : base(message)
        {
        }

        public BenchmarkerClassNotPublicException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected BenchmarkerClassNotPublicException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}
