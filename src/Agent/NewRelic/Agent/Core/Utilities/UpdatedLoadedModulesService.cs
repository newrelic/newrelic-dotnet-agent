// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
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

            SendUpdatedLoadedModules(assemblies);
        }

        private void SendUpdatedLoadedModules(IList<Assembly> assemblies)
        {

            var loadedModulesCollection = new LoadedModuleWireModelCollection();
            foreach (var assembly in assemblies)
            {
                if (!TryGetAssemblyName(assembly, out var assemblyName))
                {
                    // no way to properly track this assembly
                    continue;
                }

                var assemblyDetails = assembly.GetName();

                var loadedModule = new LoadedModuleWireModel(assemblyName, assemblyDetails.Version.ToString());

                loadedModule.Data.Add("namespace", assemblyDetails.Name);

                if (TryGetPublicKeyToken(assemblyDetails, out var publicKey))
                {
                    loadedModule.Data.Add("publicKeyToken", publicKey);
                }

                if (TryGetShaFileHashes(assembly, out var sha1FileHash, out var sha512FileHash))
                {
                    loadedModule.Data.Add("sha1Checksum", sha1FileHash);
                    loadedModule.Data.Add("sha512Checksum", sha512FileHash);
                }

                if (TryGetAssemblyHashCode(assembly, out var assemblyHashCode))
                {
                    loadedModule.Data.Add("assemblyHashCode", assemblyHashCode);
                }
                if (TryGetCompanyName(assembly, out var companyName))
                {
                    loadedModule.Data.Add("Implementation-Vendor", companyName);
                }
                if (TryGetCopyright(assembly, out var copyright))
                {
                    loadedModule.Data.Add("copyright", copyright);
                }

                // Use the .Name here and in GetLoadedModules
                loadedModulesCollection.LoadedModules.Add(loadedModule);
            }

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

        private bool TryGetAssemblyName(Assembly assembly, out string assemblyName)
        {
            try
            {
                if (assembly.IsDynamic)
                {
                    assemblyName = assembly.GetName().Name;
                }
                else
                {
                    assemblyName = Path.GetFileName(assembly.Location);
                }

                return true;
            }
            catch
            {
                assemblyName = null;
                return false;
            }
        }

        private bool TryGetPublicKeyToken(AssemblyName assemblyDetails, out string publicKey)
        {
            try
            {
                publicKey = BitConverter.ToString(assemblyDetails.GetPublicKeyToken()).Replace("-","");
                if (string.IsNullOrWhiteSpace(publicKey))
                {
                    return false;
                }

                return true;
            }
            catch
            {
                publicKey = null;
                return false;
            }
        }

        private bool TryGetShaFileHashes(Assembly assembly, out string sha1FileHash, out string sha512FileHash)
        {
            try
            {
                var location = assembly.Location;
                if (string.IsNullOrEmpty(location))
                {
                    sha1FileHash = null;
                    sha512FileHash = null;
                    return false;
                }

                if (!File.Exists(location))
                {
                    sha1FileHash = null;
                    sha512FileHash = null;
                    return false;
                }

                using (var stream = File.OpenRead(location))
                {
                    using (var sha1 = SHA1.Create())
                    {
                        var hash = sha1.ComputeHash(stream);
                        sha1FileHash = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                    }

                    // reset stream position to allow another read
                    stream.Position = 0;

                    using (var sha512 = SHA512.Create())
                    {
                        var hash = sha512.ComputeHash(stream);
                        sha512FileHash = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                    }
                }

                return true;
            }
            catch
            {
                sha1FileHash = null;
                sha512FileHash = null;
                return false;
            }
        }

        private bool TryGetAssemblyHashCode(Assembly assembly, out string assemblyHashCode)
        {
            try
            {
                assemblyHashCode = assembly.GetHashCode().ToString();
                return true;
            }
            catch
            {
                assemblyHashCode = null;
                return false;
            }
        }

        private bool TryGetCompanyName(Assembly assembly, out string companyName)
        {
            try
            {
                var attributes = assembly.GetCustomAttributes(typeof(AssemblyCompanyAttribute), true);
                if (attributes.Length < 1)
                {
                    companyName = null;
                    return false;
                }

                companyName = ((AssemblyCompanyAttribute)attributes[0]).Company;
                return true;
            }
            catch
            {
                companyName = null;
                return false;
            }
        }

        private bool TryGetCopyright(Assembly assembly, out string copyright)
        {
            try
            {
                var attributes = assembly.GetCustomAttributes(typeof(AssemblyCopyrightAttribute), true);
                if (attributes.Length < 1)
                {
                    copyright = null;
                    return false;
                }

                copyright = ((AssemblyCopyrightAttribute)attributes[0]).Copyright;
                return true;
            }
            catch
            {
                copyright = null;
                return false;
            }
        }
    }
}
