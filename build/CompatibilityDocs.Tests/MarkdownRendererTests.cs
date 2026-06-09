using System.Collections.Generic;
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
                            Packages = { new Package { Id = "Npgsql", NugetUrl = "https://www.nuget.org/packages/Npgsql/", Tabs = { "core", "framework" }, MinVersion = VersionSpec.Single("4.0.0") } },
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

        var versions = new Dictionary<(string, Platform), string>
        {
            { ("npgsql", Platform.Core), "7.0.7" },
            { ("npgsql", Platform.Framework), "7.0.7" },
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

| Library | NuGet package | Supported versions | Min agent version | Notes |
| --- | --- | --- | --- | --- |
| PostgreSQL | [Npgsql](https://www.nuget.org/packages/Npgsql/) | 4.0.0 – 7.0.7 | — | <ul><li>Prior versions may be instrumented.</li></ul> |

No server-process data is collected.

### App frameworks

- ASP.NET Core MVC: 6.0, 7.0, 8.0 (min agent v10.0.0)

## .NET Framework

### Datastores

Instruments these datastores:

| Library | NuGet package | Supported versions | Min agent version | Notes |
| --- | --- | --- | --- | --- |
| PostgreSQL | [Npgsql](https://www.nuget.org/packages/Npgsql/) | 4.0.0 – 7.0.7 | — | <ul><li>Prior versions may be instrumented.</li></ul> |

No server-process data is collected.
""";

        Assert.That(md.Replace("\r\n", "\n").TrimEnd(), Is.EqualTo(expected.Replace("\r\n", "\n").TrimEnd()));
    }

    [Test]
    public void Render_NoteTextWithEmbeddedNewlines_StaysOnOneTableRow()
    {
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
                            Packages = { new Package { Id = "CouchbaseNetClient", Tabs = { "core" }, MinVersion = VersionSpec.Single("3.2.0") } },
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

        var versions = new Dictionary<(string, Platform), string>
        {
            { ("couchbasenetclient", Platform.Core), "3.6.6" },
        };

        var md = new MarkdownRenderer(new NoteRenderer()).Render(model, versions);

        Assert.That(md, Does.Contain(
            "<ul><li>Multi-line note ending with a newline.</li><li>Versions 3.2.0+ supported since agent v10.40.0.</li></ul> |"));
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
            .Render(model, new Dictionary<(string, Platform), string>())
            .Replace("\r\n", "\n");

        Assert.That(md, Does.Contain(
            "| HttpClient | — | — | — | <details><summary>Instrumented methods (2)</summary><ul><li><code>SendAsync</code></li><li><code>GetAsync</code></li></ul></details> |"));
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
                            Packages = { new Package { Id = "RabbitMQ.Client", Tabs = { "core" }, MinVersion = VersionSpec.Single("3.5.2") } },
                            Methods = { "IModel.BasicGet", "IModel.BasicPublish" },
                            Notes = { new Note { Type = "freeform", Text = "Only EventingBasicConsumer is instrumented." } }
                        }
                    }
                }
            }
        };

        var versions = new Dictionary<(string, Platform), string>
        {
            { ("rabbitmq.client", Platform.Core), "7.1.2" },
        };

        var md = new MarkdownRenderer(new NoteRenderer()).Render(model, versions).Replace("\r\n", "\n");

        Assert.That(md, Does.Contain(
            "| 3.5.2 – 7.1.2 | — | <ul><li>Only EventingBasicConsumer is instrumented.</li></ul><details><summary>Instrumented methods (2)</summary><ul><li><code>IModel.BasicGet</code></li><li><code>IModel.BasicPublish</code></li></ul></details> |"));
    }

    [Test]
    public void Render_KnownMinUnknownLatest_ShowsMinAlone()
    {
        var model = new CompatibilityModel
        {
            Categories =
            {
                new Category
                {
                    Key = "datastores", Title = "Datastores", Tabs = { "framework" },
                    Libraries =
                    {
                        new Library
                        {
                            Name = "Memcached",
                            Packages = { new Package { Id = "EnyimMemcachedCore", Tabs = { "framework" }, MinVersion = VersionSpec.Single("2.0.0") } }
                        }
                    }
                }
            }
        };

        var md = new MarkdownRenderer(new NoteRenderer())
            .Render(model, new Dictionary<(string, Platform), string>())
            .Replace("\r\n", "\n");

        Assert.That(md, Does.Contain("| Memcached | EnyimMemcachedCore | 2.0.0 | — |"));
    }

    [Test]
    public void Render_PerPlatformMin_RendersDifferentFloorPerTab()
    {
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
                            Name = "Couchbase",
                            Packages =
                            {
                                new Package
                                {
                                    Id = "CouchbaseNetClient", Tabs = { "core", "framework" },
                                    MinVersion = VersionSpec.Map(new Dictionary<string, string> { ["core"] = "3.2.0", ["framework"] = "2.0.0" })
                                }
                            }
                        }
                    }
                }
            }
        };

        var versions = new Dictionary<(string, Platform), string>
        {
            { ("couchbasenetclient", Platform.Core), "3.6.6" },
            { ("couchbasenetclient", Platform.Framework), "3.6.6" },
        };

        var md = new MarkdownRenderer(new NoteRenderer()).Render(model, versions).Replace("\r\n", "\n");

        Assert.That(md, Does.Contain("| Couchbase | CouchbaseNetClient | 3.2.0 – 3.6.6 |"));
        Assert.That(md, Does.Contain("| Couchbase | CouchbaseNetClient | 2.0.0 – 3.6.6 |"));
    }
}
