// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using NewRelic.Agent.Api;

namespace NewRelic.Agent.Extensions.Llm
{
    public static class SupportabilityHelpers
    {
        private const string OpenAiDateRemovalPattern = @"-\d{4}-\d{2}-\d{2}";

        private static readonly ConcurrentDictionary<string, object> _seenModels = new();

        public static void CreateModelIdSupportabilityMetricsForOpenAi(string model, IAgent agent)
        {
            if (string.IsNullOrWhiteSpace(model))
            {
                return;
            }

            // Only want to send this metric once-ish per model
            if (!_seenModels.TryAdd(model, null))
            {
                return;
            }

            try
            {
                // Example openai: o1
                // Example openai: gpt-4o-2024-11-20
                var noDateModel = Regex.Replace(model, OpenAiDateRemovalPattern, string.Empty);
                var modelIdDetails = noDateModel.Split('-');
                if (modelIdDetails.Length == 1)
                {
                    agent.RecordSupportabilityMetric("DotNet/LLM/openai/" + modelIdDetails[0]);
                    return;
                }

                agent.RecordSupportabilityMetric("DotNet/LLM/openai/" + modelIdDetails[0] + "-" + modelIdDetails[1]);
            }
            catch (Exception ex) // if there is a problem, this will also only happen once-ish per model
            {
                agent.Logger.Finest($"Error creating model supportability metric for {model}: {ex.Message}");
            }
        }

        public static void CreateModelIdSupportabilityMetricsForBedrock(string model, IAgent agent)
        {
            if (string.IsNullOrWhiteSpace(model))
            {
                return;
            }

            // Only want to send this metric once-ish per model
            if (!_seenModels.TryAdd(model, null))
            {
                return;
            }

            try
            {
                // Example foundation bedrock: anthropic.claude-3-5-sonnet-20241022-v2:0
                // Example inference bedrock: us.anthropic.claude-3-5-sonnet-20241022-v2:0
                // Example bedrock marketplace: deepseek-llm-r1
                var modelDetails = model.Split('.');
                if (modelDetails.Length == 1) // bedrock marketplace
                {
                    // Format the bedrock marketplace model id into one that can be used by the standard logic.
                    var marketplaceDetails = modelDetails[0].Split('-');
                    modelDetails =
                    [
                        marketplaceDetails[0],
                        string.Join("-", marketplaceDetails, 1, marketplaceDetails.Length - 1)
                    ];
                }

                if (modelDetails.Length != 2 && modelDetails.Length != 3)
                {
                    return;
                }

                // if there is a region, it will be the first part of the model id
                var vendorIndex = modelDetails.Length == 2 ? 0 : 1;
                var vendor = modelDetails[vendorIndex];

                var modelIdDetails = modelDetails[vendorIndex + 1].Split(':')[0].Split('-');
                if (modelIdDetails[0] == "nova" || modelIdDetails[0] == "titan" || modelIdDetails[0] == "claude") // first 2 - capture some extra details to narrow down support
                {
                    agent.RecordSupportabilityMetric("DotNet/LLM/" + vendor + "/" + modelIdDetails[0] + "-" + modelIdDetails[1]);
                }
                else // first only - any model that doesn't need the above extra details
                {
                    agent.RecordSupportabilityMetric("DotNet/LLM/" + vendor + "/" + modelIdDetails[0]);
                }
            }
            catch (Exception ex) // if there is a problem, this will also only happen once-ish per model
            {
                agent.Logger.Finest($"Error creating model supportability metric for {model}: {ex.Message}");
            }
        }
    }
}
