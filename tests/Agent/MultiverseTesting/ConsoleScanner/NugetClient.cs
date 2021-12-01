// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.IO;
using System.Linq;
using System.Threading;
using NuGet.Common;
using NuGet.Packaging;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace NewRelic.Agent.ConsoleScanner
{
    public class NugetClient
    {
        private const string V3URL= "https://api.nuget.org/v3/index.json";

        private ILogger _logger;
        private CancellationToken _cancellationToken;
        private SourceCacheContext _sourceCache;
        private SourceRepository _sourceRepository;
        private string _destinationPath;

        public NugetClient(string destinationPath)
        {
            _logger = NullLogger.Instance;
            _cancellationToken = CancellationToken.None;
            _sourceCache = new SourceCacheContext();
            _sourceRepository = Repository.Factory.GetCoreV3(V3URL);
            _destinationPath = destinationPath;
        }

        public string GetLatestVersion(string packageName, bool includePrerelease = false)
        {
            var resource = _sourceRepository.GetResourceAsync<FindPackageByIdResource>().Result;
            var versions = resource.GetAllVersionsAsync(
                packageName,
                _sourceCache,
                _logger,
                _cancellationToken).Result;

            if (includePrerelease)
            {
                return versions.OrderBy(v => v).LastOrDefault().ToNormalizedString();
            }

            return versions.Where(v => !v.IsPrerelease).OrderBy(v => v).LastOrDefault().ToNormalizedString();
        }
        public string DownloadPackage(string packageName, string packageVersion)
        {
            var resource = _sourceRepository.GetResourceAsync<FindPackageByIdResource>().Result;
            var packageId = packageName;
            using (var packageStream = new MemoryStream())
            {
                _ = resource.CopyNupkgToStreamAsync(
                    packageId,
                    new NuGetVersion(packageVersion),
                    packageStream,
                    _sourceCache,
                    _logger,
                    _cancellationToken).Result;

                using (var packageReader = new PackageArchiveReader(packageStream))
                {
                    var files = packageReader.GetFiles();
                    foreach (var file in files)
                    {
                        if (File.Exists($@"{_destinationPath}{Path.DirectorySeparatorChar}{packageName.ToLower()}.{packageVersion}{Path.DirectorySeparatorChar}{file}"))
                        {
                            continue;
                        }

                       _ =  packageReader.ExtractFile(file, $@"{_destinationPath}{Path.DirectorySeparatorChar}{packageName.ToLower()}.{packageVersion}{Path.DirectorySeparatorChar}{file}", _logger);
                    }
                }
            }

            return $@"{_destinationPath}{Path.DirectorySeparatorChar}{packageName.ToLower()}.{packageVersion}";
        }
    }
}
