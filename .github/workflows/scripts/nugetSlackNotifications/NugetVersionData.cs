using System;

namespace nugetSlackNotifications
{
    public class NugetVersionData
    {
        public string PackageName { get; set; }
        public string OldVersion { get; set; }
        public string NewVersion { get; set; }
        public Version NewVersionAsVersion { get; set; }
        public string Url { get; set; }
        public DateTime PublishDate { get; set; }

        public NugetVersionData(string packageName, string oldVersion, string newVersion, string url, DateTime publishDate)
        {
            PackageName = packageName;
            OldVersion = oldVersion;
            NewVersion = newVersion;
            NewVersionAsVersion = new Version(newVersion);
            Url = url;
            PublishDate = publishDate;
        }
    }
}
