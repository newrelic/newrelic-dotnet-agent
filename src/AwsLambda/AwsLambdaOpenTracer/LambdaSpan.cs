/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
using NewRelic.OpenTracing.AmazonLambda.Events;
using NewRelic.OpenTracing.AmazonLambda.Util;
using OpenTracing;
using OpenTracing.Tag;
using OTTag = OpenTracing.Tag;
using System;
using System.Collections.Generic;
using System.Threading;

namespace NewRelic.OpenTracing.AmazonLambda
{
    internal class LambdaSpan : Event, ISpan
    {
        protected LambdaSpanContext _context;
        private string _operationName;
        private readonly string _type;
        private readonly string _guid;

        private string _parentId;
        private int _isFinished = 0;
        protected TimeSpan _duration = TimeSpan.Zero;

        private IDictionary<string, object> _intrinsics;
        private IDictionary<string, object> _userAttributes;
        private IDictionary<string, object> _agentAttributes;

        public IDictionary<string, object> Tags { get; }
        public IDictionary<string, LogEntry> SpanLog { get; }

        public LambdaRootSpan RootSpan { get; }

        public LambdaSpan(string operationName, DateTimeOffset timestamp, IDictionary<string, object> tags, LambdaSpan parentSpan, string guid)
        {
            _type = "Span";
            _operationName = operationName;
            TimeStamp = timestamp;
            Tags = tags;
            _guid = guid;
            SpanLog = new Dictionary<string, LogEntry>();

            if (parentSpan != null)
            {
                _parentId = parentSpan.Guid();
                RootSpan = parentSpan.RootSpan;
            }
            else
            {
                //This is the root span
                _parentId = null;
                RootSpan = (LambdaRootSpan)this;
            }
        }

        public DateTimeOffset TimeStamp { get; }

        public string Guid()
        {
            return _guid;
        }

        public double GetDurationInSeconds()
        {
            // Schema requires duration to be in seconds
            return (double)_duration.Ticks / (double)TimeSpan.TicksPerSecond;
        }

        public string GetOperationName()
        {
            return _operationName;
        }

        public void SetContext(LambdaSpanContext context)
        {
            _context = context;
        }

        public ISpanContext Context => _context;

        // Per 12-4-2019 spec, span events have very specific instrinsic attributes
        public override IDictionary<string, object> Intrinsics
        {
            get
            {
                return _intrinsics ?? (_intrinsics = BuildIntrinsics());
            }
        }

        protected virtual IDictionary<string, object> BuildIntrinsics()
        {
            var category = DetectSpanCategory();
            var intrinsics = new Dictionary<string, object>
            {
                { "category", category.ToString().ToLower() },
                { "type", _type },
                { "timestamp", TimeStamp.ToUnixTimeMilliseconds() },
                { "duration", GetDurationInSeconds() },
                { "guid", _guid },
                { "name", _operationName },
                { "priority", RootSpan.PrioritySamplingState.Priority },
                { "sampled", RootSpan.PrioritySamplingState.Sampled },
                { "traceId", RootSpan.DistributedTracingState.InboundPayload?.TraceId ?? RootSpan.TransactionState.TransactionId },
				//DT attributes
				{ "parent.type", RootSpan.DistributedTracingState.InboundPayload?.Type},
                { "parent.account", RootSpan.DistributedTracingState.InboundPayload?.AccountId},
                { "parent.app", RootSpan.DistributedTracingState.InboundPayload?.AppId},
                { "parent.transportType", "Unknown"},
                { "parent.transportDuration", RootSpan.DistributedTracingState.TransportDurationInMillis}
            };

            if (!string.IsNullOrEmpty(_parentId))
            {
                intrinsics.Add("parentId", _parentId);
            }
            // check if parentId is available from incoming DT payload
            else
            {
                var incomingParentId = RootSpan.DistributedTracingState.InboundPayload?.Guid;
                if (!string.IsNullOrEmpty(incomingParentId))
                {
                    intrinsics.Add("parentId", incomingParentId);
                }
            }

            if (!string.IsNullOrEmpty(RootSpan.TransactionState.TransactionId))
            {
                intrinsics.Add("transactionId", RootSpan.TransactionState.TransactionId);
            }

            if (category == SpanCategory.DATASTORE || category == SpanCategory.HTTP)
            {
                intrinsics.Add("span.kind", "client");
            }

            return intrinsics;
        }

        // Per 12-4-2019 spec, span events do not have user attributes
        public override IDictionary<string, object> UserAttributes
        {
            get
            {
                return _userAttributes ?? (_userAttributes = new Dictionary<string, object>());
            }
        }

        // Per 12-4-2019 spec, span events have very specific agent attributes
        public override IDictionary<string, object> AgentAttributes
        {
            get
            {
                return _agentAttributes ?? (_agentAttributes = Tags.BuildAgentAttributes());
            }
        }

        public void Finish()
        {
            Finish(DateTimeOffset.UtcNow);
        }

        public void Finish(DateTimeOffset finishTimestamp)
        {
            FinishInternal(finishTimestamp);
        }

        protected virtual bool FinishInternal(DateTimeOffset finishTimestamp)
        {
            var didFinish = false;
            if (Interlocked.CompareExchange(ref _isFinished, 1, 0) == 0)
            {
                didFinish = true;
                _duration = finishTimestamp - TimeStamp;
                RootSpan.Collector.SpanFinished(this);
            }

            return didFinish;
        }

        private SpanCategory DetectSpanCategory()
        {
            if (Tags.ContainsKey("db.instance")
                || Tags.ContainsKey("db.statement")
                || Tags.ContainsKey("db.type")
                || Tags.ContainsKey("db.user")
                || (Tags.ContainsKey(OTTag.Tags.Component.Key) && Tags[OTTag.Tags.Component.Key].Equals("DynamoDB")))
            {
                return SpanCategory.DATASTORE;
            }

            if (Tags.ContainsKey("http.method") || Tags.ContainsKey("http.status_code") || Tags.ContainsKey("http.url"))
            {
                return SpanCategory.HTTP;
            }

            return SpanCategory.GENERIC;
        }

        public object GetTag(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return null;
            }

            Tags.TryGetValue(key, out var ret);
            return ret;
        }

        public string GetBaggageItem(string key)
        {
            throw new NotImplementedException();
        }

        public ISpan Log(IEnumerable<KeyValuePair<string, object>> fields)
        {
            return Log(DateTimeOffset.UtcNow, fields);
        }

        public ISpan Log(DateTimeOffset timestamp, IEnumerable<KeyValuePair<string, object>> fields)
        {
            foreach (var entry in fields)
            {
                LogInternal(timestamp, entry.Key, entry.Value);
            }

            return this;
        }

        public ISpan Log(string @event)
        {
            return Log(DateTimeOffset.UtcNow, @event);
        }

        public ISpan Log(DateTimeOffset timestamp, string @event)
        {
            return LogInternal(DateTimeOffset.UtcNow, "event", @event);
        }

        private ISpan LogInternal(DateTimeOffset timestamp, string eventName, object value)
        {
            if (value != null)
            {
                if ("event".Equals(eventName) && "error".Equals(value))
                {
                    RootSpan.TransactionState.SetError();
                }

                SpanLog.Add(eventName, new LogEntry(timestamp, value));
            }

            return this;
        }

        public ISpan SetBaggageItem(string key, string value)
        {
            throw new NotImplementedException();
        }

        public ISpan SetOperationName(string operationName)
        {
            _operationName = operationName;
            return this;
        }

        public ISpan SetTag(string key, string value) => SetTagInternal(key, value);

        public ISpan SetTag(string key, bool value) => SetTagInternal(key, value);

        public ISpan SetTag(string key, int value) => SetTagInternal(key, value);

        public ISpan SetTag(string key, double value) => SetTagInternal(key, value);

        public ISpan SetTag(BooleanTag tag, bool value) => SetTagInternal(tag.Key, value);

        public ISpan SetTag(IntOrStringTag tag, string value) => SetTagInternal(tag.Key, value);

        public ISpan SetTag(IntTag tag, int value) => SetTagInternal(tag.Key, value);

        public ISpan SetTag(StringTag tag, string value) => SetTagInternal(tag.Key, value);

        public LogEntry GetSpanLogEntry(string eventName)
        {
            SpanLog.TryGetValue(eventName, out var ret);
            return ret;
        }

        private ISpan SetTagInternal(string key, object value)
        {
            Tags[key] = value;
            return this;
        }
    }

    internal enum SpanCategory
    {
        HTTP,
        DATASTORE,
        GENERIC
    }
}
