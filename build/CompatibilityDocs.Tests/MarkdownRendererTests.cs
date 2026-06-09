using System.Collections.Generic;
using CompatibilityDocs.Derivation;
using CompatibilityDocs.Rendering;
using CompatibilityDocs.Schema;
using NUnit.Framework;

namespace CompatibilityDocs.Tests;

[TestFixture]
public class MarkdownRendererTests
{
    [Test]
    public void Render_ProducesDeterministicDocument()
    {
        var model = new CompatibilityModel
        {
            Categories =
            {
                new Category
                {
                    Key = "datastores", Title = "Datastores", Tabs = { "core", "framework" },
                    Intro = "Instruments these datastores:",
                    Footnotes = { "No server-process data is collected." },
                    Libraries =
                    {
                        new Library
                        {
                            Name = "PostgreSQL",
                            Packages = { new Package { Id = "Npgsql", NugetUrl = "https://www.nuget.org/packages/Npgsql/", Tabs = { "core", "framework" } } },
                            Notes = { new Note { Type = "freeform", Text = "Prior versions may be instrumented." } }
                        }
                    }
                },
                new Category
                {
                    Key = "app-frameworks", Title = "App frameworks", Tabs = { "core" },
                    Libraries =
                    {
                        new Library { Name = "ASP.NET Core MVC", SupportedVersions = new() { "6.0", "7.0", "8.0" }, MinAgentVersion = "10.0.0" }
                    }
                }
            }
        };

        var versions = new Dictionary<(string, Platform), VersionRange>
        {
            { ("npgsql", Platform.Core), new VersionRange("4.0.0", "7.0.7") },
            { ("npgsql", Platform.Framework), new VersionRange("4.0.0", "7.0.7") },
        };

        var md = new MarkdownRenderer(new NoteRenderer()).Render(model, versions);

        var expected =
"""
<!-- GENERATED FILE — do not edit by hand.
     Source: build/CompatibilityDocs/compatibility.yaml
     Regenerate: dotnet run --project build/CompatibilityDocs -->

# .NET agent automatic instrumentation compatibility

## Contents
- [.NET Core](#net-core) — [Datastores](#datastores) · [App frameworks](#app-frameworks)
- [.NET Framework](#net-framework) — [Datastores](#datastores-1)

## .NET Core

### Datastores

Instruments these datastores:

| Library | NuGet package | Versions tested | Min agent version | Notes |
| --- | --- | --- | --- | --- |
| PostgreSQL | [Npgsql](https://www.nuget.org/packages/Npgsql/) | 4.0.0 – 7.0.7 | — | Prior versions may be instrumented. |

No server-process data is collected.

### App frameworks

- ASP.NET Core MVC: 6.0, 7.0, 8.0 (min agent v10.0.0)

## .NET Framework

### Datastores

Instruments these datastores:

| Library | NuGet package | Versions tested | Min agent version | Notes |
| --- | --- | --- | --- | --- |
| PostgreSQL | [Npgsql](https://www.nuget.org/packages/Npgsql/) | 4.0.0 – 7.0.7 | — | Prior versions may be instrumented. |

No server-process data is collected.
""";

        Assert.That(md.Replace("\r\n", "\n").TrimEnd(), Is.EqualTo(expected.Replace("\r\n", "\n").TrimEnd()));
    }

    [Test]
    public void Render_NoteTextWithEmbeddedNewlines_StaysOnOneTableRow()
    {
        // A YAML block scalar (> or |) can leave a trailing or embedded newline in the
        // note text. The renderer must keep the table cell on a single line so the row
        // stays a valid markdown table row.
        var model = new CompatibilityModel
        {
            Categories =
            {
                new Category
                {
                    Key = "datastores", Title = "Datastores", Tabs = { "core" },
                    Libraries =
                    {
                        new Library
                        {
                            Name = "Couchbase",
                            Packages = { new Package { Id = "CouchbaseNetClient", Tabs = { "core" } } },
                            Notes =
                            {
                                new Note { Type = "freeform", Text = "Multi-line note ending with a newline.\n" },
                                new Note { Type = "addedInAgent", SinceVersion = "3.2.0", AgentVersion = "10.40.0" }
                            }
                        }
                    }
                }
            }
        };

        var versions = new Dictionary<(string, Platform), VersionRange>
        {
            { ("couchbasenetclient", Platform.Core), new VersionRange("3.5.1", "3.6.6") },
        };

        var md = new MarkdownRenderer(new NoteRenderer()).Render(model, versions);

        Assert.That(md, Does.Contain(
            "Multi-line note ending with a newline.<br>Versions 3.2.0+ supported since agent v10.40.0. |"));
    }

    [Test]
    public void Render_MethodOnlyLibrary_RendersMethodsInNotesColumn()
    {
        var model = new CompatibilityModel
        {
            Categories =
            {
                new Category
                {
                    Key = "external-calls", Title = "External call libraries", Tabs = { "core" },
                    Libraries =
                    {
                        new Library { Name = "HttpClient", Methods = { "SendAsync", "GetAsync" } }
                    }
                }
            }
        };

        var md = new MarkdownRenderer(new NoteRenderer())
            .Render(model, new Dictionary<(string, Platform), VersionRange>())
            .Replace("\r\n", "\n");

        // Method-only library appears as a table row (dash for the versions column); the
        // methods go in the Notes column inside a collapsed <details> block.
        Assert.That(md, Does.Contain(
            "| HttpClient | — | — | — | <details><summary>Instrumented methods (2)</summary><br><code>SendAsync</code><br><code>GetAsync</code></details> |"));
    }

    [Test]
    public void Render_LibraryWithNotesAndMethods_AppendsMethodsAfterNotes()
    {
        var model = new CompatibilityModel
        {
            Categories =
            {
                new Category
                {
                    Key = "messaging", Title = "Message systems", Tabs = { "core" },
                    Libraries =
                    {
                        new Library
                        {
                            Name = "RabbitMQ",
                            Packages = { new Package { Id = "RabbitMQ.Client", Tabs = { "core" } } },
                            Methods = { "IModel.BasicGet", "IModel.BasicPublish" },
                            Notes = { new Note { Type = "freeform", Text = "Only EventingBasicConsumer is instrumented." } }
                        }
                    }
                }
            }
        };

        var versions = new Dictionary<(string, Platform), VersionRange>
        {
            { ("rabbitmq.client", Platform.Core), new VersionRange("5.2.0", "7.1.2") },
        };

        var md = new MarkdownRenderer(new NoteRenderer()).Render(model, versions).Replace("\r\n", "\n");

        Assert.That(md, Does.Contain(
            "| 5.2.0 – 7.1.2 | — | Only EventingBasicConsumer is instrumented.<br><details><summary>Instrumented methods (2)</summary><br><code>IModel.BasicGet</code><br><code>IModel.BasicPublish</code></details> |"));
    }
}
