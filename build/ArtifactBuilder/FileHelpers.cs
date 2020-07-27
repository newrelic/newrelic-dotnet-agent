using System;
using System.Collections.Generic;
using System.IO;

namespace ArtifactBuilder
{
    public static class FileHelpers
    {
        public static void CopyFile(string filePath, string directory)
        {
            Directory.CreateDirectory(directory);
            var fileInfo = new System.IO.FileInfo(filePath);
            System.IO.File.Copy(fileInfo.FullName, $@"{directory}\{fileInfo.Name}", true);
        }

        public static void CopyFile(IEnumerable<string> filePaths, string directory)
        {
            foreach (var file in filePaths)
            {
                CopyFile(file, directory);
            }
        }

        public static void CopyAll(string sourceDir, string targetDir)
        {
            Directory.CreateDirectory(targetDir);

            foreach (var file in Directory.GetFiles(sourceDir))
            {
                File.Copy(file, Path.Combine(targetDir, Path.GetFileName(file)));
            }

            foreach (var directory in Directory.GetDirectories(sourceDir))
            {
                CopyAll(directory, Path.Combine(targetDir, Path.GetFileName(directory)));
            }
        }

        public static void DeleteDirectories(params string[] directories)
        {
            foreach (var dir in directories)
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
            }
        }

        public static string GetSha256Checksum(string file)
        {
            using (var stream = File.OpenRead(file))
            {
                var sha = new System.Security.Cryptography.SHA256Managed();
                byte[] checksum = sha.ComputeHash(stream);
                return BitConverter.ToString(checksum).Replace("-", String.Empty);
            }
        }

        public static void ReplaceTextInFile(string path, IDictionary<string, string> replacements)
        {
            var contents = File.ReadAllText(path);
            foreach (var item in replacements)
            {
                contents = contents.Replace(item.Key, item.Value);
            }
            File.WriteAllText(path, contents);
        }
    }
}
