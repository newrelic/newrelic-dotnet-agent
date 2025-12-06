// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using NewRelic.IntegrationTests.Models;
using Newtonsoft.Json;

namespace MockNewRelic.Controllers
{
    [Route("agent_listener")]
    public class AgentListenerController : Controller
    {
        private const string ContentEncodingHeader = "Content-Encoding";
        private const string ReqeustHeaderKey = "NR-Session";
        private const string ReqeustHeaderValue = "TestHeaderValue";
        private static readonly Dictionary<string, string> _requestHeaderMap = new Dictionary<string, string> { { ReqeustHeaderKey, ReqeustHeaderValue } };
        private static readonly string EmptyResponse = "{}";

        private static List<CollectedRequest> _collectedRequests = new List<CollectedRequest>();
        private static List<AgentCommand> _queuedCommands = new List<AgentCommand>();

        private static string _liveInstrumentation =
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
            "<extension xmlns=\"urn:newrelic-extension\">" +
            "<instrumentation>" +
            "<tracerFactory metricName=\"Live/CustomMethodDefaultTracer\">" +
            "<match assemblyName=\"BasicMvcApplication\" className=\"BasicMvcApplication.Controllers.CustomInstrumentationController\">" +
            "<exactMethodMatcher methodName=\"CustomMethodDefaultTracer\" />" +
            "</match>" +
            "</tracerFactory>" +
            "</instrumentation>" +
            "</extension>";

        private static bool _setLiveInstrumentationOnConnect = false;
        private static HeaderValidationData _headerValidationData = new HeaderValidationData();

        [HttpGet]
        [Route("WarmUpCollector")]
        public string WarmUpCollector()
        {
            return "All systems go.";
        }

        [HttpGet]
        [HttpPost]
        [HttpPut]
        [Route("invoke_raw_method")]
        public string InvokeRawMethod([FromBody] byte[] body)
        {
            var capturedRequest = CaptureRequest(body);
            _collectedRequests.Add(capturedRequest);

            var collectorMethod = capturedRequest.Querystring
                .FirstOrDefault(qs => qs.Key == "method").Value;

            switch (collectorMethod)
            {
                case "preconnect":
                    {
                        var preconnectResponse = new Dictionary<string, object>
                        {
                            ["redirect_host"] = Request.Host.Host
                        };
                        var host = new CollectorResponseEnvelope<Dictionary<string, object>>(null, preconnectResponse);

                        return JsonConvert.SerializeObject(host);
                    }
                case "connect":
                    {
                        var serverConfig = new Dictionary<string, object>();

                        serverConfig["agent_run_id"] = Guid.NewGuid();
                        serverConfig["entity_guid"] = Guid.NewGuid();

                        if (_setLiveInstrumentationOnConnect)
                        {
                            var configObj = new Dictionary<string, object>
                            {
                                ["config"] = _liveInstrumentation,
                                ["name"] = "live_instrumentation"
                            };

                            var instrumentations = new List<Dictionary<string, object>>
                        {
                            configObj
                        };

                            serverConfig["instrumentation"] = instrumentations;
                        }

                        serverConfig["request_headers_map"] = _requestHeaderMap;

                        var config = new CollectorResponseEnvelope<Dictionary<string, object>>(null, serverConfig);
                        return JsonConvert.SerializeObject(config);
                    }
                case "get_agent_commands":
                    {
                        var commands = _queuedCommands;
                        _queuedCommands = new List<AgentCommand>();

                        var result = new CollectorResponseEnvelope<List<AgentCommand>>(null, commands);
                        return JsonConvert.SerializeObject(result);
                    }
                case "metric_data":
                    {
                        var defaultNRSessonHeaderValue = HttpContext.Request.Headers[ReqeustHeaderKey].ToString();
                        _headerValidationData.MetricDataHasMap = (!string.IsNullOrEmpty(defaultNRSessonHeaderValue) && defaultNRSessonHeaderValue == ReqeustHeaderValue) ? true : false;
                        return EmptyResponse;
                    }
                case "analytic_event_data":
                    {
                        var defaultNRSessonHeaderValue = HttpContext.Request.Headers[ReqeustHeaderKey].ToString();
                        _headerValidationData.AnalyticEventDataHasMap = (!string.IsNullOrEmpty(defaultNRSessonHeaderValue) && defaultNRSessonHeaderValue == ReqeustHeaderValue) ? true : false;
                        return EmptyResponse;
                    }
                case "transaction_sample_data":
                    {
                        var defaultNRSessonHeaderValue = HttpContext.Request.Headers[ReqeustHeaderKey].ToString();
                        _headerValidationData.TransactionSampleDataHasMap = (!string.IsNullOrEmpty(defaultNRSessonHeaderValue) && defaultNRSessonHeaderValue == ReqeustHeaderValue) ? true : false;
                        return EmptyResponse;
                    }
                case "span_event_data":
                    {
                        var defaultNRSessonHeaderValue = HttpContext.Request.Headers[ReqeustHeaderKey].ToString();
                        _headerValidationData.SpanEventDataHasMap = (!string.IsNullOrEmpty(defaultNRSessonHeaderValue) && defaultNRSessonHeaderValue == ReqeustHeaderValue) ? true : false;
                        return EmptyResponse;
                    }
            }

            return EmptyResponse;
        }

        private CollectedRequest CaptureRequest(byte[] body)
        {
            var collectedRequest = new CollectedRequest();

            collectedRequest.RequestBody = body;
            collectedRequest.Method = Request.Method;
            collectedRequest.Querystring = Request.Query.Select(q => new KeyValuePair<string, string>(q.Key, q.Value));
            collectedRequest.ContentEncoding = Request.Headers[ContentEncodingHeader];

            return collectedRequest;
        }

        [HttpGet]
        [Route("CollectedRequests")]
        public List<CollectedRequest> CollectedRequests()
        {
            return _collectedRequests;
        }

        [HttpGet]
        [Route("TriggerThreadProfile")]
        public void TriggerThreadProfile()
        {
            var threadProfileArguments = new Dictionary<string, object>();
            threadProfileArguments["profile_id"] = -1;
            threadProfileArguments["sample_period"] = 0.1F; //Agent enforces minimums
            threadProfileArguments["duration"] = 30; //Override is setup for tests so this value will work

            var threadProfileDetails = new CommandDetails("start_profiler", threadProfileArguments);
            var threadProfileCommand = new AgentCommand(-1, threadProfileDetails);

            _queuedCommands.Add(threadProfileCommand);
        }

        [HttpGet]
        [Route("TriggerCustomInstrumentationEditorAgentCommand")]
        public void TriggerCustomInstrumentationEditorAgentCommand()
        {
            var configObj = new Dictionary<string, object>
            {
                ["config"] = _liveInstrumentation
            };

            var liveInstrumentationAgentCommandArguments = new Dictionary<string, object>
            {
                ["instrumentation"] = configObj
            };

            var liveInstrumentationAgentCommandDetails = new CommandDetails("instrumentation_update", liveInstrumentationAgentCommandArguments);
            var liveInstrumentationAgentCommand = new AgentCommand(-1, liveInstrumentationAgentCommandDetails);

            _queuedCommands.Add(liveInstrumentationAgentCommand);
        }

        [HttpGet]
        [Route("SetCustomInstrumentationEditorOnConnect")]
        public string SetCustomInstrumentationEditorOnConnect()
        {
            _setLiveInstrumentationOnConnect = true;
            return "_setLiveInstrumentationOnConnect was enabled";
        }

        [HttpGet]
        [Route("HeaderValidation")]
        public string HeaderValidation()
        {
            return JsonConvert.SerializeObject(_headerValidationData);
        }
    }
}
