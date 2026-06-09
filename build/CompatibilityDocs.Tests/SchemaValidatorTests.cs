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
}
