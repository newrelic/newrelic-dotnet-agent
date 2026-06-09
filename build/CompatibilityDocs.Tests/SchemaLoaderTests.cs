using System.Linq;
using CompatibilityDocs.Schema;
using NUnit.Framework;

namespace CompatibilityDocs.Tests;

[TestFixture]
public class SchemaLoaderTests
{
    private const string Yaml = """
categories:
  - key: datastores
    title: Datastores
    tabs: [core, framework]
    intro: "Instruments these datastores:"
    footnotes:
      - No server-process data is collected.
    libraries:
      - name: PostgreSQL
        packages:
          - id: Npgsql
            nugetUrl: https://www.nuget.org/packages/Npgsql/
            tabs: [core, framework]
        notes:
          - type: freeform
            text: Prior versions may be instrumented.
      - name: Elasticsearch
        packages:
          - id: Elastic.Clients.Elasticsearch
            tabs: [core, framework]
            notes:
              - { type: maxSupportedVersion, version: "8.15.10" }
          - id: NEST
            tabs: [core, framework]
      - name: MongoDB (legacy driver)
        tabs: [framework]
        packages:
          - id: mongocsharpdriver
            versionSource: manual
            minVersion: "1.10.0"
            latestVersion: "1.10.0"
""";

    [Test]
    public void Load_ParsesCategoriesLibrariesPackagesAndNotes()
    {
        var model = new SchemaLoader().LoadFromString(Yaml);

        Assert.That(model.Categories, Has.Count.EqualTo(1));
        var cat = model.Categories[0];
        Assert.That(cat.Key, Is.EqualTo("datastores"));
        Assert.That(cat.Tabs, Is.EquivalentTo(new[] { "core", "framework" }));
        Assert.That(cat.Footnotes, Has.Count.EqualTo(1));

        var pg = cat.Libraries.Single(l => l.Name == "PostgreSQL");
        Assert.That(pg.Packages[0].Id, Is.EqualTo("Npgsql"));
        Assert.That(pg.Packages[0].VersionSource, Is.EqualTo("derived")); // default
        Assert.That(pg.Notes[0].Type, Is.EqualTo("freeform"));

        var es = cat.Libraries.Single(l => l.Name == "Elasticsearch");
        Assert.That(es.Packages[0].Notes[0].Type, Is.EqualTo("maxSupportedVersion"));
        Assert.That(es.Packages[0].Notes[0].Version, Is.EqualTo("8.15.10"));

        var mongo = cat.Libraries.Single(l => l.Name == "MongoDB (legacy driver)");
        Assert.That(mongo.Tabs, Is.EquivalentTo(new[] { "framework" }));
        Assert.That(mongo.Packages[0].VersionSource, Is.EqualTo("manual"));
        Assert.That(mongo.Packages[0].MinVersion!.For("framework"), Is.EqualTo("1.10.0"));
    }
}
