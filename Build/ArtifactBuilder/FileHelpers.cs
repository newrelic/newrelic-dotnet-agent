using System.Collections.Generic;
using System.IO;

namespace ArtifactBuilder
{
    public static class FileHelpers
    {
        public static void CopyFile(string filePath, string directory)
        {
            var fileInfo = new System.IO.FileInfo(filePath);
            System.IO.File.Copy(fileInfo.FullName, $@"{directory}\{fileInfo.Name}");
        }

        public static void CopyFile(IEnumerable<string> filePaths, string directory)
        {
            foreach (var file in filePaths)
            {
                CopyFile(file, directory);
            }
        }

        public static void CopyAll(string sourceDirectory, string destinationPath)
        {
            //Now Create all of the directories
            foreach (string dirPath in Directory.GetDirectories(sourceDirectory, "*", SearchOption.AllDirectories))
            {
                Directory.CreateDirectory(dirPath.Replace(sourceDirectory, destinationPath));
            }

            //Copy all the files & Replaces any files with the same name
            foreach (string newPath in Directory.GetFiles(sourceDirectory, "*.*", SearchOption.AllDirectories))
            {
                File.Copy(newPath, newPath.Replace(sourceDirectory, destinationPath), true);
            }
        }
    }
}