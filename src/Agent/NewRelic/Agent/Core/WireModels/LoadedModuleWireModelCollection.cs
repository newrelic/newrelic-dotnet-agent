// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using NewRelic.Agent.Core.JsonConverters;
using Newtonsoft.Json;

namespace NewRelic.Agent.Core.WireModels
{
    [JsonConverter(typeof(LoadedModuleWireModelCollectionJsonConverter))]
    public class LoadedModuleWireModelCollection
    {
        public List<LoadedModuleWireModel> LoadedModules { get; }

        private LoadedModuleWireModelCollection()
        {
            LoadedModules = new List<LoadedModuleWireModel>();
        }

        public void Clear()
        {
            LoadedModules.Clear();
        }

        public static LoadedModuleWireModelCollection Build(IList<Assembly> assemblies)
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

            return loadedModulesCollection;
        }

        private static bool TryGetAssemblyName(Assembly assembly, out string assemblyName)
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

                if (string.IsNullOrWhiteSpace(assemblyName))
                {
                    return false;
                }

                return true;
            }
            catch
            {
                assemblyName = null;
                return false;
            }
        }

        private static bool TryGetPublicKeyToken(AssemblyName assemblyDetails, out string publicKey)
        {
            try
            {
                publicKey = BitConverter.ToString(assemblyDetails.GetPublicKeyToken()).Replace("-", "");
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

        private static bool TryGetShaFileHashes(Assembly assembly, out string sha1FileHash, out string sha512FileHash)
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

        private static bool TryGetAssemblyHashCode(Assembly assembly, out string assemblyHashCode)
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

        private static bool TryGetCompanyName(Assembly assembly, out string companyName)
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

        private static bool TryGetCopyright(Assembly assembly, out string copyright)
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
