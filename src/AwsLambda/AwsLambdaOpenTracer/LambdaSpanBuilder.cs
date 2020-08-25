// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.OpenTracing.AmazonLambda.State;
using NewRelic.OpenTracing.AmazonLambda.Util;
using NewRelic.Core;
using OpenTracing;
using OpenTracing.Tag;
using System;
using System.Collections.Generic;

namespace NewRelic.OpenTracing.AmazonLambda
{
    internal class LambdaSpanBuilder : ISpanBuilder
    {
        private readonly ILogger _logger;
        private readonly IFileSystemManager _fileSystemManager;
        private long _startTimeInTicks;
        private bool _ignoreActiveSpan = false;
        private ISpanContext _parent;
        private readonly string _operationName;
        private readonly IDictionary<string, object> _tags = new Dictionary<string, object>();

        public LambdaSpanBuilder(string operationName)
        {
            _logger = new Logger();
            this._operationName = operationName;
            _fileSystemManager = new FileSystemManager();
        }

        public ISpanBuilder AddReference(string referenceType, ISpanContext referencedContext)
        {
            if (_parent == null && (referenceType.Equals(References.ChildOf) || referenceType.Equals(References.FollowsFrom)))
            {
                this._parent = referencedContext;
            }
            return this;
        }

        public ISpanBuilder AsChildOf(ISpanContext parent)
        {
            return AddReference(References.ChildOf, parent);
        }

        public ISpanBuilder AsChildOf(ISpan parent)
        {
            return AddReference(References.ChildOf, parent.Context);
        }

        public ISpanBuilder IgnoreActiveSpan()
        {
            throw new NotImplementedException();
        }

        public ISpan Start()
        {
            return StartManual();
        }

        public IScope StartActive()
        {
            return StartActive(true);
        }

        public IScope StartActive(bool finishSpanOnDispose)
        {
            return LambdaTracer.Instance.ScopeManager.Activate(StartManual(), finishSpanOnDispose);
        }

        public ISpanBuilder WithStartTimestamp(DateTimeOffset timestamp)
        {
            _startTimeInTicks = timestamp.Ticks;
            return this;
        }

        public ISpanBuilder WithTag(string key, string value)
        {
            _tags.Add(key, value);
            return this;
        }

        public ISpanBuilder WithTag(string key, bool value)
        {
            _tags.Add(key, value);
            return this;
        }

        public ISpanBuilder WithTag(string key, int value)
        {
            _tags.Add(key, value);
            return this;
        }

        public ISpanBuilder WithTag(string key, double value)
        {
            _tags.Add(key, value);
            return this;
        }

        public ISpanBuilder WithTag(BooleanTag tag, bool value)
        {
            _tags.Add(tag.Key, value);
            return this;
        }

        public ISpanBuilder WithTag(IntOrStringTag tag, string value)
        {
            _tags.Add(tag.Key, value);
            return this;
        }

        public ISpanBuilder WithTag(IntTag tag, int value)
        {
            _tags.Add(tag.Key, value);
            return this;
        }

        public ISpanBuilder WithTag(StringTag tag, string value)
        {
            _tags.Add(tag.Key, value);
            return this;
        }

        public ISpan StartManual()
        {
            LambdaTracer tracer = LambdaTracer.Instance as LambdaTracer;
            ISpan activeSpan = tracer.ActiveSpan;

            ISpanContext parentSpanContext = null;
            if (!_ignoreActiveSpan && _parent == null && activeSpan != null)
            {
                AddReference(References.ChildOf, activeSpan.Context);
                parentSpanContext = activeSpan.Context;
            }
            else if (_parent != null)
            {
                parentSpanContext = _parent;
            }

            LambdaSpan parentSpan = null;
            if (parentSpanContext is LambdaSpanContext)
            {
                parentSpan = ((LambdaSpanContext)parentSpanContext).GetSpan();
            }

            var guid = GuidGenerator.GenerateNewRelicGuid();
            LambdaSpan newSpan;

            if (parentSpan != null)
            {
                newSpan = new LambdaSpan(_operationName, DateTimeOffset.UtcNow, _tags, parentSpan, guid);
            }
            else
            {
                var rootSpan = new LambdaRootSpan(_operationName, DateTimeOffset.UtcNow, _tags, guid, new DataCollector(_logger, tracer.DebugMode, _fileSystemManager), new TransactionState(), new PrioritySamplingState(), new DistributedTracingState());

                if (parentSpanContext is LambdaPayloadContext payloadContext)
                {
                    rootSpan.ApplyLambdaPayloadContext(payloadContext);
                }
                else
                {
                    rootSpan.ApplyAdaptiveSampling(tracer.AdaptiveSampler);
                }

                newSpan = rootSpan;
            }

            LambdaSpanContext spanContext = new LambdaSpanContext(newSpan);
            newSpan.SetContext(spanContext);

            return newSpan;
        }
    }
}
