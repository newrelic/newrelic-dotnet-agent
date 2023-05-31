// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.Core.DataTransport;
using NewRelic.Agent.Core.Time;
using NewRelic.Agent.Core.WireModels;

namespace NewRelic.Agent.Core.Utilities
{
    public class UpdatedLoadedModulesService : DisposableService
    {
        private static readonly TimeSpan _timeBetweenExecutions = TimeSpan.FromMinutes(1);

        private readonly IList<string> _loadedModulesSeen = new List<string>();
        private readonly IScheduler _scheduler;
        private readonly IDataTransportService _dataTransportService;

        public UpdatedLoadedModulesService(IScheduler scheduler, IDataTransportService dataTransportService)
        {
            _dataTransportService = dataTransportService;
            _scheduler = scheduler;
            _scheduler.ExecuteEvery(GetLoadedModules, _timeBetweenExecutions);
        }

        private void GetLoadedModules()
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Where(assembly => assembly != null)
                .Where(assembly => !_loadedModulesSeen.Contains(assembly.GetName().Name))
#if NETFRAMEWORK
                .Where(assembly => !(assembly is System.Reflection.Emit.AssemblyBuilder))
#endif
                .ToList();

            if (assemblies.Count < 1)
            {
                return;
            }

            var loadedModulesCollection = LoadedModuleWireModelCollection.Build(assemblies);

            SendUpdatedLoadedModules(loadedModulesCollection);
        }

        private void SendUpdatedLoadedModules(LoadedModuleWireModelCollection loadedModulesCollection)
        {
            var responseStatus = _dataTransportService.Send(loadedModulesCollection);
            if (responseStatus != DataTransportResponseStatus.RequestSuccessful)
            {
                // Try again next time
                return;
            }

            foreach (var module in loadedModulesCollection.LoadedModules)
            {
                _loadedModulesSeen.Add(module.Data["namespace"].ToString());
            }
        }
    }
}
