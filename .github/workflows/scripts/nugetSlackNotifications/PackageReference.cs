using System;
using System.Text.RegularExpressions;
using System.Xml.Serialization;

namespace nugetSlackNotifications
{
    public class PackageReference
    {
        [XmlAttribute]
        public string Include { get; set; }

        [XmlAttribute]
        public string Version { get; set; }

        [XmlIgnore]
        public Version VersionAsVersion => new Version(Version);

        [XmlIgnore]
        public int LineNumber { get; set; }

        [XmlAttribute]
        public string Condition { get; set; }

        public string TargetFramework
        {
            get
            {
                if (Condition == null)
                {
                    return null;
                }
                var match = Regex.Match(Condition, @"net\d+\.*\d+");
                return match.Success ? match.Value : null;
            }
        }

        [XmlAttribute]
        public bool Pin { get; set; }
    }
}
