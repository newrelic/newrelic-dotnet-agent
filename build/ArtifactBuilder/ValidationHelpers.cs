using System;
using System.Collections.Generic;
using System.IO;

namespace ArtifactBuilder
{
    public static class ValidationHelpers
    {

        public static void ValidateComponents(SortedSet<string> expectedComponents, SortedSet<string> unpackedComponents, string artifactName)
        {
            var missingExpectedComponents = new SortedSet<string>(expectedComponents, StringComparer.OrdinalIgnoreCase);
            missingExpectedComponents.ExceptWith(unpackedComponents);
            foreach (var missingComponent in missingExpectedComponents)
            {
                throw new PackagingException($"The unpacked {artifactName} was missing the expected component {missingComponent}");
            }

            var unexpectedUnpackedComponents = new SortedSet<string>(unpackedComponents, StringComparer.OrdinalIgnoreCase);
            unexpectedUnpackedComponents.ExceptWith(expectedComponents);
            foreach (var unexpectedComponent in unexpectedUnpackedComponents)
            {
                throw new PackagingException($"The unpacked {artifactName} contained an unexpected component {unexpectedComponent}");
            }
        }

        public static SortedSet<string> GetUnpackedComponents(string installedFilesRoot)
        {
            var unpackedComponents = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

            var enumerationOptions = new EnumerationOptions
            {
                RecurseSubdirectories = true,
            };

            foreach (var file in Directory.EnumerateFiles(installedFilesRoot, "*", enumerationOptions))
            {
                unpackedComponents.Add(file);
            }

            return unpackedComponents;
        }

        public static void AddFilesToCollectionWithNewPath(SortedSet<string> fileCollection, string newPath, IEnumerable<string> filesWithPath)
        {
            foreach (var fileWithPath in filesWithPath)
            {
                AddSingleFileToCollectionWithNewPath(fileCollection, newPath, fileWithPath);
            }
        }

        public static void AddSingleFileToCollectionWithNewPath(SortedSet<string> fileCollection, string newPath, string fileWithPath)
        {
            fileCollection.Add(Path.Join(newPath, GetFileNameWithoutPath(fileWithPath)));
        }

        public static string GetFileNameWithoutPath(string fullFileName)
        {
            var fileInfo = new FileInfo(fullFileName);
            return fileInfo.Name;
        }

    }
}
