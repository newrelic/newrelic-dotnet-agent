using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using SevenZipExtractor;

namespace ArtifactBuilder.Artifacts
{
    public class LinuxPackage : Artifact
    {
        private readonly string _packagePlatform;
        private readonly string _fileExtension;
        private readonly string _buildOutputDirectory;
        private readonly string _packagePath;
        private readonly string _packageName;

        private AgentComponents _coreAgentComponents;

        public LinuxPackage(string name, string packagePlatform, string fileExtension) : base(name)
        {
            _packageName = name;
            _packagePlatform = packagePlatform;
            _fileExtension = fileExtension;
            _buildOutputDirectory = $@"{RepoRootDirectory}\src\_build\CoreArtifacts";
            _packagePath = GetPackagePath();
            OutputDirectory = $@"{RepoRootDirectory}\build\BuildArtifacts\{Name}";
            ValidateContentAction = ValidateContent;

            _coreAgentComponents = AgentComponents.GetAgentComponents(AgentType.Core, "Release", "x64", RepoRootDirectory, HomeRootDirectory);
        }

        protected override void InternalBuild()
        {

            Directory.CreateDirectory(OutputDirectory);

            var fileInfo = new FileInfo(_packagePath);
            File.Copy(fileInfo.FullName, $@"{OutputDirectory}\{fileInfo.Name}", true);

            //Generate the checksum file based on the Linux standards for the checksum contents: <checksum_value> <checksum_mode><filename>
            //<checksum_mode> is either a ' ' for text mode or '*' for binary mode.
            File.WriteAllText($@"{OutputDirectory}\{fileInfo.Name}.sha256", $"{FileHelpers.GetSha256Checksum(_packagePath)} *{fileInfo.Name}");
        }

        private string GetPackagePath()
        {
            //Note that the wildcard purposely includes the separator and the version number
            var fileNameSearchPattern = $"newrelic-dotnet-agent*{_packagePlatform}.{_fileExtension}";
            var packagePath = Directory.GetFiles(_buildOutputDirectory, fileNameSearchPattern).FirstOrDefault();
            if (string.IsNullOrEmpty(packagePath))
            {
                throw new PackagingException($"The {fileNameSearchPattern} package could not be found.");
            }
            return packagePath;
        }

        private string Unpack()
        {
            var unpackedDir = Path.Join(OutputDirectory, "unpacked");
            if (Directory.Exists(unpackedDir))
            {
                FileHelpers.DeleteDirectories(unpackedDir);
            }

            var packageFileName = new FileInfo(_packagePath).Name;

            switch (_fileExtension)
            {
                case "deb":
                    using (ArchiveFile archiveFile = new ArchiveFile(_packagePath))
                    {
                        archiveFile.Extract(unpackedDir);
                    }

                    using (ArchiveFile archiveFileXz = new ArchiveFile(Path.Join(unpackedDir, "data.tar.xz")))
                    {
                        MemoryStream memoryStream = new MemoryStream();
                        archiveFileXz.Entries[0].Extract(memoryStream);

                        using (ArchiveFile archiveFileTar = new ArchiveFile(memoryStream, SevenZipFormat.Tar))
                        {
                            archiveFileTar.Extract(unpackedDir);
                        }
                    }
                    return Path.Combine(unpackedDir, "usr", "local", "newrelic-dotnet-agent");
                case "rpm":
                    using (ArchiveFile archiveFile = new ArchiveFile(_packagePath))
                    {
                        archiveFile.Extract(unpackedDir);
                    }
                    var cpioFile = packageFileName.Replace(".rpm", "") + ".cpio.xz";
                    using (ArchiveFile archiveFileXz = new ArchiveFile(Path.Join(unpackedDir, cpioFile)))
                    {
                        MemoryStream memoryStream = new MemoryStream();
                        archiveFileXz.Entries[0].Extract(memoryStream);

                        using (ArchiveFile archiveFileCpio = new ArchiveFile(memoryStream, SevenZipFormat.Cpio))
                        {
                            archiveFileCpio.Extract(unpackedDir);
                        }
                    }
                    return Path.Combine(unpackedDir, "usr", "local", "newrelic-dotnet-agent");
                case "tar.gz":
                    // The SevenZipWrapper is choking on our tar.gz file for some reason
                    Directory.CreateDirectory(unpackedDir);
                    UnGzip(_packagePath, unpackedDir);
                    using (ArchiveFile archiveFileTar = new ArchiveFile(Path.Join(unpackedDir, packageFileName.Replace(".gz", ""))))
                    {
                        archiveFileTar.Extract(unpackedDir);
                    }
                    return Path.Combine(unpackedDir, "newrelic-dotnet-agent");
                default:
                    throw new PackagingException($"Unknown extension {_fileExtension} for Linux package {_packageName}");
            }
        }

        private void UnGzip(string gzipFile, string path)
        {
            using FileStream compressedFileStream = File.Open(gzipFile, FileMode.Open);
            using FileStream outputFileStream = File.Create(Path.Join(path, new FileInfo(gzipFile).Name.Replace(".gz", "")));
            using var decompressor = new GZipStream(compressedFileStream, CompressionMode.Decompress);
            decompressor.CopyTo(outputFileStream);
        }

        private void ValidateContent()
        {
            var unpackedLocation = Unpack();

            var installedFilesRoot = unpackedLocation;

            var expectedComponents = GetExpectedComponents(installedFilesRoot);

            var unpackedComponents = ValidationHelpers.GetUnpackedComponents(installedFilesRoot);

            ValidationHelpers.ValidateComponents(expectedComponents, unpackedComponents, _packageName);

            FileHelpers.DeleteDirectories(unpackedLocation);
        }

        private SortedSet<string> GetExpectedComponents(string installedFilesRoot)
        {
            var expectedComponents = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

            ValidationHelpers.AddFilesToCollectionWithNewPath(expectedComponents, installedFilesRoot, _coreAgentComponents.RootInstallDirectoryComponents);
            ValidationHelpers.AddFilesToCollectionWithNewPath(expectedComponents, installedFilesRoot, _coreAgentComponents.AgentHomeDirComponents.Where(f => !f.Contains("NewRelic.Profiler.dll")));
            ValidationHelpers.AddSingleFileToCollectionWithNewPath(expectedComponents, installedFilesRoot, "libNewRelicProfiler.so");
            ValidationHelpers.AddFilesToCollectionWithNewPath(expectedComponents, installedFilesRoot, _coreAgentComponents.ConfigurationComponents);
            ValidationHelpers.AddSingleFileToCollectionWithNewPath(expectedComponents, installedFilesRoot, "run.sh");
            ValidationHelpers.AddSingleFileToCollectionWithNewPath(expectedComponents, installedFilesRoot, "setenv.sh");

            var netcoreExtensionsFolder = Path.Join(installedFilesRoot, "extensions");
            ValidationHelpers.AddFilesToCollectionWithNewPath(expectedComponents, netcoreExtensionsFolder, _coreAgentComponents.ExtensionDirectoryComponents);
            ValidationHelpers.AddFilesToCollectionWithNewPath(expectedComponents, netcoreExtensionsFolder, _coreAgentComponents.WrapperXmlFiles);

            return expectedComponents;
        }

    }
}
