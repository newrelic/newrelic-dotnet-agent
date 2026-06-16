using System;
using System.IO;
using CompatibilityDocs.Schema;
using NUnit.Framework;

namespace CompatibilityDocs.Tests;

[TestFixture]
public class CompatibilityYamlTests
{
    // End-to-end guard: load the real compatibility.yaml and run the validator. The curated
    // minVersion is now required on every package, so a hand-edit that drops one (or breaks
    // the schema another way) should fail here, not only at generation time.
    [Test]
    public void RealYaml_LoadsAndValidates()
    {
        var repoRoot = RepoRootLocator.Find(AppContext.BaseDirectory);
        var yamlPath = Path.Combine(repoRoot, "build", "CompatibilityDocs", "compatibility.yaml");
        Assert.That(File.Exists(yamlPath), Is.True, $"compatibility.yaml not found at {yamlPath}");

        var model = new SchemaLoader().LoadFromFile(yamlPath);

        Assert.DoesNotThrow(() => new SchemaValidator().Validate(model));
    }
}
