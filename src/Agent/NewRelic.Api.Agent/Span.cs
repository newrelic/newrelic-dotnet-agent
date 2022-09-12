// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using Microsoft.CSharp.RuntimeBinder;

namespace NewRelic.Api.Agent
{
    internal class Span : ISpan
    {
        private readonly dynamic _wrappedSpan;
        private static ISpan _noOpSpan = new NoOpSpan();

        internal Span(dynamic? wrappedSpan = null)
        {
            _wrappedSpan = wrappedSpan ?? _noOpSpan;
        }

        private static bool _isAddCustomAttributeAvailable = true;

        public ISpan AddCustomAttribute(string key, object value)
        {
            if (!_isAddCustomAttributeAvailable)
            {
                return _noOpSpan.AddCustomAttribute(key, value);
            }

            try
            {
                _wrappedSpan.AddCustomAttribute(key, value);
            }
            catch (RuntimeBinderException)
            {
                _isAddCustomAttributeAvailable = false;
            }

            return this;
        }

        private static bool _isSetNameAvailable = true;

        public ISpan SetName(string name)
        {
            if (!_isSetNameAvailable)
            {
                return _noOpSpan.SetName(name);
            }

            try
            {
                _wrappedSpan.SetName(name);
            }
            catch (RuntimeBinderException)
            {
                _isSetNameAvailable = false;
            }

            return this;
        }
    }
}
