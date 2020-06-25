/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
using NewRelic.OpenTracing.AmazonLambda.Events;
using NewRelic.OpenTracing.AmazonLambda.Traces;
using NewRelic.OpenTracing.AmazonLambda.Util;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NewRelic.OpenTracing.AmazonLambda
{
    internal class DataCollector
    {
        private LinkedList<Event> _spanReservoir = new LinkedList<Event>();
        private Errors _errors = new Errors();
        private string _executionEnv = Environment.GetEnvironmentVariable("AWS_EXECUTION_ENV");
        private ILogger _logger;
        private readonly bool _debugMode;
        private readonly object _spanReservoirLock = new object();

        public DataCollector(ILogger logger, bool debugMode)
        {
            _debugMode = debugMode;
            _logger = logger;
        }

        //Push finished spans into the reservoir. When the root span finishes, log them only if they're sampled.
        public void SpanFinished(LambdaSpan span)
        {
            lock (_spanReservoirLock)
            {
                _spanReservoir.AddFirst(span);
            }
        }

        public void TransformSpans(LambdaRootSpan rootSpan)
        {
            lock (_spanReservoirLock)
            {
                // Record errors after root span has finished. By now, txn name has been set
                foreach (LambdaSpan lambdaSpan in _spanReservoir)
                {
                    _errors.RecordErrors(lambdaSpan);
                }

                var arnTag = rootSpan.GetTag("aws.arn");
                var arn = (arnTag as string) ?? "";

                LinkedList<Event> spans;
                // Do not collect Spans if sampled=false, clear reservoir and set spans to empty list
                if (rootSpan.PrioritySamplingState.Sampled)
                {
                    spans = _spanReservoir;
                }
                else
                {
                    spans = new LinkedList<Event>();
                }

                _spanReservoir = new LinkedList<Event>();
                TransactionEvent txnEvent = new TransactionEvent(rootSpan);
                var errorEvents = _errors.GetAndClearEvents();
                var errorTraces = _errors.GetAndClearTraces();
                WriteData(arn, _executionEnv, spans.ToList(), txnEvent, errorEvents, errorTraces);
            }
        }

        // Write all the payload data to the console using standard out. This is the only method that should call the Logger#out method.
        private void WriteData(string arn, string executionEnv, IList<Event> spans, TransactionEvent txnEvent, IList<ErrorEvent> errorEvents, IList<ErrorTrace> errorTraces)
        {
            var metadata = ProtocolUtil.GetMetadata(arn, executionEnv);
            var data = ProtocolUtil.GetData(spans, txnEvent, errorEvents, errorTraces);

            var payload = new List<object> { 2, "NR_LAMBDA_MONITORING", metadata, ProtocolUtil.CompressAndEncode(JsonConvert.SerializeObject(data)) };
            _logger.Log(JsonConvert.SerializeObject(payload));
            var debug = Environment.GetEnvironmentVariable("NEW_RELIC_DEBUG_MODE");
            if (_debugMode)
            {
                _logger.Log(JsonConvert.SerializeObject(data));
            }
        }
    }
}
