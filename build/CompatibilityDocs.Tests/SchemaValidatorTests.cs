using System.Collections.Generic;
using CompatibilityDocs.Schema;
using NUnit.Framework;

namespace CompatibilityDocs.Tests;

[TestFixture]
public class SchemaValidatorTests
{
    private static CompatibilityModel ModelWith(Library lib) => new()
    {
        Categories = { new Category { Key = "datastores", Title = "Datastores", Tabs = { "core" }, Libraries = { lib } } }
    };

    [Test]
    public void Validate_UnknownNoteType_Throws_WithAllowedValues()
    {
        var model = ModelWith(new Library { Name = "X",
            Packages = { new Package { Id = "P", Tabs = { "core" }, MinVersion = VersionSpec.Single("1.0.0") } },
            Notes = { new Note { Type = "bogus" } } });

        var ex = Assert.Throws<SchemaValidationException>(() => new SchemaValidator().Validate(model));
        Assert.That(ex!.Message, Does.Contain("bogus"));
        Assert.That(ex.Message, Does.Contain("addedInAgent"));
    }

    [Test]
    public void Validate_PackageWithoutMinVersion_Throws()
    {
        var model = ModelWith(new Library { Name = "X",
            Packages = { new Package { Id = "P", Tabs = { "core" } } } }); // no MinVersion

        var ex = Assert.Throws<SchemaValidationException>(() => new SchemaValidator().Validate(model));
        Assert.That(ex!.Message, Does.Contain("minVersion"));
    }

    [Test]
    public void Validate_MinVersionMapMissingDeclaredTab_Throws()
    {
        var model = ModelWith(new Library { Name = "X",
            Packages = { new Package { Id = "P", Tabs = { "core", "framework" },
                MinVersion = VersionSpec.Map(new Dictionary<string, string> { ["core"] = "1.0.0" }) } } });

        var ex = Assert.Throws<SchemaValidationException>(() => new SchemaValidator().Validate(model));
        Assert.That(ex!.Message, Does.Contain("framework"));
    }

    [Test]
    public void Validate_MinVersionMapWithUndeclaredTabKey_Throws()
    {
        var model = ModelWith(new Library { Name = "X",
            Packages = { new Package { Id = "P", Tabs = { "core" },
                MinVersion = VersionSpec.Map(new Dictionary<string, string> { ["core"] = "1.0.0", ["framework"] = "2.0.0" }) } } });

        Assert.Throws<SchemaValidationException>(() => new SchemaValidator().Validate(model));
    }

    [Test]
    public void Validate_ManualPackageWithoutLatest_Throws()
    {
        var model = ModelWith(new Library { Name = "X",
            Packages = { new Package { Id = "P", Tabs = { "core" }, VersionSource = "manual",
                MinVersion = VersionSpec.Single("1.0.0") } } }); // no LatestVersion

        Assert.Throws<SchemaValidationException>(() => new SchemaValidator().Validate(model));
    }

    [Test]
    public void Validate_LibraryWithNeitherPackagesNorSupportedVersionsNorMethods_Throws()
    {
        var model = ModelWith(new Library { Name = "Empty" });
        Assert.Throws<SchemaValidationException>(() => new SchemaValidator().Validate(model));
    }

    [Test]
    public void Validate_AddedInAgentMissingFields_Throws()
    {
        var model = ModelWith(new Library { Name = "X",
            Packages = { new Package { Id = "P", Tabs = { "core" }, MinVersion = VersionSpec.Single("1.0.0") } },
            Notes = { new Note { Type = "addedInAgent", SinceVersion = "1.0.0" } } });

        Assert.Throws<SchemaValidationException>(() => new SchemaValidator().Validate(model));
    }

    [Test]
    public void Validate_ValidModel_DoesNotThrow()
    {
        var model = ModelWith(new Library { Name = "X",
            Packages = { new Package { Id = "P", Tabs = { "core" }, MinVersion = VersionSpec.Single("1.0.0") } },
            Notes = { new Note { Type = "addedInAgent", SinceVersion = "1.0.0", AgentVersion = "10.0.0" } } });

        Assert.DoesNotThrow(() => new SchemaValidator().Validate(model));
    }

    [Test]
    public void Validate_ManualPackageWithMinAndLatest_DoesNotThrow()
    {
        var model = ModelWith(new Library { Name = "X",
            Packages = { new Package { Id = "P", Tabs = { "core" }, VersionSource = "manual",
                MinVersion = VersionSpec.Single("1.0.0"), LatestVersion = VersionSpec.Single("2.0.0") } } });

        Assert.DoesNotThrow(() => new SchemaValidator().Validate(model));
    }

    [Test]
    public void Validate_NoteTabsWithUndeclaredTab_Throws()
    {
        // Library effective tabs = [core] (from category). Note declares [framework] — not declared.
        var model = ModelWith(new Library
        {
            Name = "X",
            Packages = { new Package { Id = "P", Tabs = { "core" }, MinVersion = VersionSpec.Single("1.0.0") } },
            Notes = { new Note { Type = "freeform", Text = "test.", Tabs = new() { "framework" } } }
        });

        var ex = Assert.Throws<SchemaValidationException>(() => new SchemaValidator().Validate(model));
        Assert.That(ex!.Message, Does.Contain("framework"), "Error must name the offending tab.");
        Assert.That(ex.Message, Does.Contain("X"), "Error must name the library.");
    }

    [Test]
    public void Validate_MinAgentVersionMapWithUndeclaredTab_Throws()
    {
        // Library effective tabs = [core]. MinAgentVersion map has a [framework] key.
        var model = ModelWith(new Library
        {
            Name = "Y",
            Packages = { new Package { Id = "P", Tabs = { "core" }, MinVersion = VersionSpec.Single("1.0.0") } },
            MinAgentVersion = VersionSpec.Map(new Dictionary<string, string> { ["core"] = "10.0.0", ["framework"] = "9.7.0" })
        });

        var ex = Assert.Throws<SchemaValidationException>(() => new SchemaValidator().Validate(model));
        Assert.That(ex!.Message, Does.Contain("framework"), "Error must name the undeclared tab.");
    }

    [Test]
    public void Validate_MinAgentVersionMapPartialCoverage_DoesNotThrow()
    {
        // Library effective tabs = [core, framework]. MinAgentVersion map covers only [core].
        // Partial coverage is ALLOWED — a tab with no entry simply renders no min-agent suffix.
        var model = new CompatibilityModel
        {
            Categories =
            {
                new Category
                {
                    Key = "datastores", Title = "Datastores", Tabs = { "core", "framework" },
                    Libraries =
                    {
                        new Library
                        {
                            Name = "Z",
                            Packages = { new Package { Id = "P", Tabs = { "core", "framework" },
                                MinVersion = VersionSpec.Single("1.0.0") } },
                            MinAgentVersion = VersionSpec.Map(new Dictionary<string, string> { ["core"] = "10.0.0" })
                        }
                    }
                }
            }
        };

        Assert.DoesNotThrow(() => new SchemaValidator().Validate(model));
    }

    [Test]
    public void Validate_PackageMinAgentVersionMapWithUndeclaredTab_Throws()
    {
        // Package declares [core]; its minAgentVersion map has a [framework] key.
        var model = ModelWith(new Library
        {
            Name = "Y",
            Packages =
            {
                new Package
                {
                    Id = "P", Tabs = { "core" }, MinVersion = VersionSpec.Single("1.0.0"),
                    MinAgentVersion = VersionSpec.Map(new Dictionary<string, string> { ["core"] = "10.35.0", ["framework"] = "10.36.0" })
                }
            }
        });

        var ex = Assert.Throws<SchemaValidationException>(() => new SchemaValidator().Validate(model));
        Assert.That(ex!.Message, Does.Contain("framework"), "Error must name the undeclared tab.");
        Assert.That(ex.Message, Does.Contain("P"), "Error must name the package.");
    }

    [Test]
    public void Validate_PackageNoteTabsWithUndeclaredTab_Throws()
    {
        var model = ModelWith(new Library
        {
            Name = "X",
            Packages =
            {
                new Package
                {
                    Id = "P", Tabs = { "core" }, MinVersion = VersionSpec.Single("1.0.0"),
                    Notes = { new Note { Type = "freeform", Text = "test.", Tabs = new() { "framework" } } }
                }
            }
        });

        var ex = Assert.Throws<SchemaValidationException>(() => new SchemaValidator().Validate(model));
        Assert.That(ex!.Message, Does.Contain("framework"), "Error must name the offending tab.");
        Assert.That(ex.Message, Does.Contain("P"), "Error must name the package.");
    }
}
