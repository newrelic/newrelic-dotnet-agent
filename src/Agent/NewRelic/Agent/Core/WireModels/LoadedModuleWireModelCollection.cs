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

                return !string.IsNullOrWhiteSpace(assemblyName);
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
                return !string.IsNullOrWhiteSpace(publicKey);
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

                using (var fs = new FileStream(location, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    var sha1 = SHA1.Create();
                    var sha512 = SHA512.Create();
                    var buffer = new byte[4096]; // 4KB
                    int bytesRead;
                    while ((bytesRead = fs.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        sha1.TransformBlock(buffer, 0, bytesRead, null, 0);
                        sha512.TransformBlock(buffer, 0, bytesRead, null, 0);
                    }

                    sha1.TransformFinalBlock(buffer, 0, 0);
                    sha512.TransformFinalBlock(buffer, 0, 0);
                    sha1FileHash = BitConverter.ToString(sha1.Hash).Replace("-", "").ToLowerInvariant();
                    sha512FileHash = BitConverter.ToString(sha512.Hash).Replace("-", "").ToLowerInvariant();
                    sha1.Dispose();
                    sha512.Dispose();
                }

                return !string.IsNullOrWhiteSpace(sha1FileHash) && !string.IsNullOrWhiteSpace(sha512FileHash);
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
                return !string.IsNullOrWhiteSpace(assemblyHashCode);
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
                return !string.IsNullOrWhiteSpace(companyName);
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
                return !string.IsNullOrWhiteSpace(copyright);
            }
            catch
            {
                copyright = null;
                return false;
            }
        }
    }
}
