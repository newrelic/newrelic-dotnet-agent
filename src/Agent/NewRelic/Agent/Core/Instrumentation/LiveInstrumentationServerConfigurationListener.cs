// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Core.Events;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Core.Logging;
using System;

namespace NewRelic.Agent.Core.Instrumentation
{
    public class LiveInstrumentationServerConfigurationListener : ConfigurationBasedService
    {
        private readonly IInstrumentationService _instrumentationService;

        public LiveInstrumentationServerConfigurationListener(IInstrumentationService instrumentationService)
        {
            _instrumentationService = instrumentationService;
            _subscriptions.Add<ServerConfigurationUpdatedEvent>(OnServerConfigurationUpdated);
        }

        protected override void OnConfigurationUpdated(ConfigurationUpdateSource configurationUpdateSource)
        {
            // This implementation only exists to satisfy the derivation from ConfigurationBasedService, which exists for access to
            // to the customInstrumentationEditor configuration option, which is influenced by the highSecurity configuration.
        }

        private void OnServerConfigurationUpdated(ServerConfigurationUpdatedEvent serverConfigurationUpdatedEvent)
        {
            var instrumentation = serverConfigurationUpdatedEvent.Configuration.Instrumentation;

            if (instrumentation != null && !instrumentation.IsEmpty())
            {
                if (_configuration.CustomInstrumentationEditorEnabled)
                {
                    try
                    {
                        foreach (var instrumentationSet in instrumentation)
                        {
                            _instrumentationService.AddOrUpdateLiveInstrumentation(instrumentationSet.Name, instrumentationSet.Config);
                        }

                        Log.Info("Applying live instrumentation from Custom Instrumentation Editor.");

                        // We want to apply custom instrumentation regardless of whether or not any was received on
                        // this connect because we may have received instrumentation on a previous connect.
                        _instrumentationService.ApplyInstrumentation();
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "OnServerConfigurationUpdated() failed");
                    }
                }
                else
                {
                    Log.Warn("Live instrumentation received from server Custom Instrumentation Editor not applied due to configuration.");
                    var liveInstrumentationCleared = _instrumentationService.ClearLiveInstrumentation();
                    if (liveInstrumentationCleared)
                    {
                        Log.Info("Clearing out existing live instrumentation because the configuration was previously enabled, but it is now disabled.");
                        _instrumentationService.ApplyInstrumentation();
                    }
                }
            }
        }
    }
}
