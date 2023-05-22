using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Formats;
using System.Formats.Tar;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Microsoft.Win32;

namespace ArtifactBuilder.Artifacts
{
    public class LinuxPackage : Artifact
    {
        private const string GzipFileExtension = ".gz";
        private const string Default7zPath = @"C:\Program Files\7-Zip\7z.exe";

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
            _buildOutputDirectory = Path.Join(RepoRootDirectory, "src", "_build", "CoreArtifacts");
            _packagePath = GetPackagePath();
            OutputDirectory = Path.Join(RepoRootDirectory, "build", "BuildArtifacts", Name);
            ValidateContentAction = ValidateContent;

            _coreAgentComponents = AgentComponents.GetAgentComponents(AgentType.Core, "Release", "x64", RepoRootDirectory, HomeRootDirectory);
        }

        protected override void InternalBuild()
        {

            Directory.CreateDirectory(OutputDirectory);

            var fileInfo = new FileInfo(_packagePath);
            File.Copy(fileInfo.FullName, Path.Join(OutputDirectory, fileInfo.Name), true);

            //Generate the checksum file based on the Linux standards for the checksum contents: <checksum_value> <checksum_mode><filename>
            //<checksum_mode> is either a ' ' for text mode or '*' for binary mode.
            File.WriteAllText(Path.Join(OutputDirectory, $"{fileInfo.Name}.sha256"), $"{FileHelpers.GetSha256Checksum(_packagePath)} *{fileInfo.Name}");
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

            switch (_fileExtension)
            {
                case "deb":
                    UnpackWith7z(_packagePath, unpackedDir, false); // creates "data.tar" in unpackedDir
                    UnpackWith7z(Path.Join(unpackedDir, "data.tar"), unpackedDir, true);
                    return Path.Combine(unpackedDir, "usr", "local", "newrelic-dotnet-agent");
                case "rpm":
                    UnpackWith7z(_packagePath, unpackedDir, false); // creates cpio file in unpackedDir with same base name as the .rpm
                    var cpioFile = Path.Join(unpackedDir, new FileInfo(_packagePath).Name.Replace(".rpm", "") + ".cpio");
                    UnpackWith7z(cpioFile, unpackedDir, true);
                    return Path.Combine(unpackedDir, "usr", "local", "newrelic-dotnet-agent");
                case "tar.gz":
                    Directory.CreateDirectory(unpackedDir);
                    var tar = UnGzip(_packagePath, unpackedDir);
                    TarFile.ExtractToDirectory(tar, unpackedDir, true);
                    return Path.Combine(unpackedDir, "newrelic-dotnet-agent");
                default:
                    throw new PackagingException($"Unknown extension {_fileExtension} for Linux package {_packageName}");
            }
        }

        private string UnGzip(string gzipFile, string outputPath)
        {
            if (!gzipFile.EndsWith(GzipFileExtension))
            {
                throw new ArgumentException($"Input filename {gzipFile} does not end in '{GzipFileExtension}'");
            }
            var outputFile = Path.Join(outputPath, new FileInfo(gzipFile).Name.Replace(GzipFileExtension, ""));

            using FileStream compressedFileStream = File.Open(gzipFile, FileMode.Open);
            using FileStream outputFileStream = File.Create(outputFile);
            using var decompressor = new GZipStream(compressedFileStream, CompressionMode.Decompress);
            decompressor.CopyTo(outputFileStream);
            return outputFile;
        }

        private void UnpackWith7z(string archiveFile, string outputPath, bool extractWithFullPaths)
        {
            var pathTo7z = Get7zPath();
            if (string.IsNullOrEmpty(pathTo7z))
            {
                throw new PackagingException("Unable to find path to 7z tool, can't validate contents of Linux archives.");
            }

            var operation = extractWithFullPaths ? "x" : "e";
            var parameters = $"{operation} {archiveFile} -o{outputPath}";
            var pInfo = new ProcessStartInfo(pathTo7z, parameters);
            pInfo.CreateNoWindow = true;
            pInfo.RedirectStandardOutput = true;
            pInfo.RedirectStandardError = true;
            pInfo.UseShellExecute = false;

            var process = Process.Start(pInfo);
            process.WaitForExit(30000);
            if (!process.HasExited)
            {
                process.Kill();
                throw new Exception($"7z unpackage failed to complete in a timely fashion.");
            }
            if (process.ExitCode != 0)
            {
                throw new Exception($"7z unpackage failed with exit code {process.ExitCode}.");
            }
        }

        private string Get7zPath()
        {
            if (File.Exists(Default7zPath))
            {
                return Default7zPath;
            }
            if (OperatingSystem.IsWindows())
            {
                var maybePath = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\7-Zip", "Path", string.Empty) as string;
                if (File.Exists(maybePath))
                {
                    return maybePath;
                }
            }
            return string.Empty;
        }

        private void ValidateContent()
        {
            var installedFilesRoot = Unpack();

            var expectedComponents = GetExpectedComponents(installedFilesRoot);

            var unpackedComponents = ValidationHelpers.GetUnpackedComponents(installedFilesRoot);

            ValidationHelpers.ValidateComponents(expectedComponents, unpackedComponents, _packageName);

            FileHelpers.DeleteDirectories(installedFilesRoot);
        }

        private SortedSet<string> GetExpectedComponents(string installedFilesRoot)
        {
            var expectedComponents = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

            ValidationHelpers.AddFilesToCollectionWithNewPath(expectedComponents, installedFilesRoot, _coreAgentComponents.RootInstallDirectoryComponents);
            ValidationHelpers.AddFilesToCollectionWithNewPath(expectedComponents, installedFilesRoot, _coreAgentComponents.AgentHomeDirComponents.Where(f => f != _coreAgentComponents.WindowsProfiler));
            ValidationHelpers.AddSingleFileToCollectionWithNewPath(expectedComponents, installedFilesRoot, _coreAgentComponents.LinuxProfiler);
            ValidationHelpers.AddFilesToCollectionWithNewPath(expectedComponents, installedFilesRoot, _coreAgentComponents.ConfigurationComponents);
            // These next two files are added to the Linux packages by the containerized build process, not the ArtifactBuilder, so they are hardcoded here
            ValidationHelpers.AddSingleFileToCollectionWithNewPath(expectedComponents, installedFilesRoot, "run.sh");
            ValidationHelpers.AddSingleFileToCollectionWithNewPath(expectedComponents, installedFilesRoot, "setenv.sh");

            var netcoreExtensionsFolder = Path.Join(installedFilesRoot, "extensions");
            ValidationHelpers.AddFilesToCollectionWithNewPath(expectedComponents, netcoreExtensionsFolder, _coreAgentComponents.ExtensionDirectoryComponents);
            ValidationHelpers.AddFilesToCollectionWithNewPath(expectedComponents, netcoreExtensionsFolder, _coreAgentComponents.WrapperXmlFiles);

            return expectedComponents;
        }

    }
}
