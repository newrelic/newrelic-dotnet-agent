using System.IO;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace CompatibilityDocs.Schema;

public class SchemaLoader
{
    private readonly IDeserializer _deserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .WithTypeConverter(new VersionSpecYamlConverter())
        .IgnoreUnmatchedProperties()
        .Build();

    public virtual CompatibilityModel LoadFromString(string yaml)
        => _deserializer.Deserialize<CompatibilityModel>(yaml) ?? new CompatibilityModel();

    public virtual CompatibilityModel LoadFromFile(string path)
        => LoadFromString(File.ReadAllText(path));
}
