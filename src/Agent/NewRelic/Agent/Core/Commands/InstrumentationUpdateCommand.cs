// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using NewRelic.Agent.Core.Configuration;
using NewRelic.Agent.Core.Instrumentation;
using NewRelic.Core.Logging;
using Newtonsoft.Json.Linq;

namespace NewRelic.Agent.Core.Commands
{
    public class InstrumentationUpdateCommand : AbstractCommand
    {
        private readonly IInstrumentationService _instrumentationService;

        public InstrumentationUpdateCommand(IInstrumentationService instrumentationService)
        {
            Name = "instrumentation_update";
            _instrumentationService = instrumentationService;
        }

        public override object Process(IDictionary<string, object> arguments)
        {
            var errorMessage = InstrumentationUpdate(arguments);
            if (errorMessage == null)
            {
                return new Dictionary<string, object>();
            }

            Log.Error(errorMessage);

            // Other commands send errors under the error key, but I've verified the UI uses `errors`
            // Originally noticed this in the java agent code: https://source.datanerd.us/java-agent/java_agent/blob/master/newrelic-agent/src/main/java/com/newrelic/agent/reinstrument/ReinstrumentResult.java
            return new Dictionary<string, object>
            {
                {"errors", errorMessage}
            };
        }

        private string InstrumentationUpdate(IDictionary<string, object> arguments)
        {
            if (arguments.TryGetValue("instrumentation", out var instrumentationSetObject))
            {
                if (instrumentationSetObject is JObject instrumentationSet)
                {
                    try
                    {
                        var instrumentationConfig = instrumentationSet.ToObject<ServerConfiguration.InstrumentationConfig>();
                        _instrumentationService.AddOrUpdateLiveInstrumentation(instrumentationConfig.Name, instrumentationConfig.Config);
                        _instrumentationService.ApplyInstrumentation();
                    }
                    catch (Exception e)
                    {
                        Log.Error(e, "The instrumentation update was malformed");
                        return "The instrumentation update was malformed";
                    }
                }
                else
                {
                    return "The instrumentation update instrumentation set was empty";
                }
            }
            else
            {
                return "The instrumentation key was missing";
            }

            Log.Debug($"{Name} command complete.");
            return null;
        }
    }
}
