// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;

namespace NewRelic.Agent.Core.Utilities
{
    public class TypeInstantiatorResult<T>
    {

        public readonly IEnumerable<T> Instances;


        public readonly IEnumerable<Exception> Exceptions;

        public TypeInstantiatorResult(IEnumerable<T> instances = null, IEnumerable<Exception> exceptions = null)
        {
            Instances = instances ?? Enumerable.Empty<T>();
            Exceptions = exceptions ?? Enumerable.Empty<Exception>();
        }
    }
}
