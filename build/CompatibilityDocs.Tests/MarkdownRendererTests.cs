// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
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
                        new Library { Name = "ASP.NET Core MVC", SupportedVersions = new() { "6.0", "7.0", "8.0" }, MinAgentVersion = VersionSpec.Single("10.0.0") }
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

    [Test]
    public void Render_NoteWithFrameworkOnlyTabs_AppearsInFrameworkNotCore()
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
                                    MinVersion = VersionSpec.Map(new Dictionary<string, string>
                                        { ["core"] = "3.2.0", ["framework"] = "2.0.0" })
                                }
                            },
                            Notes =
                            {
                                new Note { Type = "freeform", Text = "Framework-only note.", Tabs = new() { "framework" } }
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

        var fwSection = md[md.IndexOf("## .NET Framework", StringComparison.Ordinal)..];
        Assert.That(fwSection, Does.Contain("Framework-only note."),
            "Framework-only note must appear in the Framework section.");

        var coreSection = md[md.IndexOf("## .NET Core", StringComparison.Ordinal)..md.IndexOf("## .NET Framework", StringComparison.Ordinal)];
        Assert.That(coreSection, Does.Not.Contain("Framework-only note."),
            "Framework-only note must NOT appear in the Core section.");
    }

    [Test]
    public void Render_NoteWithNullTabs_AppearsUnderBothTabs()
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
                            Name = "PostgreSQL",
                            Packages =
                            {
                                new Package
                                {
                                    Id = "Npgsql", Tabs = { "core", "framework" },
                                    MinVersion = VersionSpec.Single("4.0.0")
                                }
                            },
                            Notes =
                            {
                                new Note { Type = "freeform", Text = "Shared note.", Tabs = null }
                            }
                        }
                    }
                }
            }
        };

        var versions = new Dictionary<(string, Platform), string>
        {
            { ("npgsql", Platform.Core), "7.0.7" },
            { ("npgsql", Platform.Framework), "7.0.7" },
        };

        var md = new MarkdownRenderer(new NoteRenderer()).Render(model, versions).Replace("\r\n", "\n");

        var fwSection = md[md.IndexOf("## .NET Framework", StringComparison.Ordinal)..];
        var coreSection = md[md.IndexOf("## .NET Core", StringComparison.Ordinal)..md.IndexOf("## .NET Framework", StringComparison.Ordinal)];

        Assert.That(coreSection, Does.Contain("Shared note."), "Null-tabs note must appear in Core section.");
        Assert.That(fwSection, Does.Contain("Shared note."), "Null-tabs note must appear in Framework section.");
    }

    [Test]
    public void Render_PerTabMinAgentVersion_RendersDifferentValuePerTab()
    {
        var model = new CompatibilityModel
        {
            Categories =
            {
                new Category
                {
                    Key = "logging", Title = "Logging frameworks", Tabs = { "core", "framework" },
                    Libraries =
                    {
                        new Library
                        {
                            Name = "Microsoft.Extensions.Logging",
                            MinAgentVersion = VersionSpec.Map(new Dictionary<string, string>
                                { ["core"] = "10.0.0", ["framework"] = "9.7.0" }),
                            Packages =
                            {
                                new Package
                                {
                                    Id = "Microsoft.Extensions.Logging", Tabs = { "core", "framework" },
                                    MinVersion = VersionSpec.Single("3.0.0")
                                }
                            }
                        }
                    }
                }
            }
        };

        var versions = new Dictionary<(string, Platform), string>
        {
            { ("microsoft.extensions.logging", Platform.Core), "9.0.0" },
            { ("microsoft.extensions.logging", Platform.Framework), "9.0.0" },
        };

        var md = new MarkdownRenderer(new NoteRenderer()).Render(model, versions).Replace("\r\n", "\n");

        var fwSection = md[md.IndexOf("## .NET Framework", StringComparison.Ordinal)..];
        var coreSection = md[md.IndexOf("## .NET Core", StringComparison.Ordinal)..md.IndexOf("## .NET Framework", StringComparison.Ordinal)];

        Assert.That(coreSection, Does.Contain("| 3.0.0 – 9.0.0 | 10.0.0 |"),
            "Core row must show 10.0.0 in the min-agent column.");
        Assert.That(fwSection, Does.Contain("| 3.0.0 – 9.0.0 | 9.7.0 |"),
            "Framework row must show 9.7.0 in the min-agent column.");
    }

    [Test]
    public void Render_NotesOnlyLibrary_RendersSingleDashRowWithNotes()
    {
        // A library with no packages, supportedVersions, or methods — only notes (IBM DB2).
        // It must render one table row with dashes for package/versions/min-agent and the
        // note in the Notes cell.
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
                            Name = "IBM DB2", Tabs = new() { "framework" },
                            Notes = { new Note { Type = "freeform", Text = "Supported on .NET Framework." } }
                        }
                    }
                }
            }
        };

        var md = new MarkdownRenderer(new NoteRenderer())
            .Render(model, new Dictionary<(string, Platform), string>())
            .Replace("\r\n", "\n");

        Assert.That(md, Does.Contain(
            "| IBM DB2 | — | — | — | <ul><li>Supported on .NET Framework.</li></ul> |"));
    }

    [Test]
    public void Render_PackageMinAgentVersion_OverridesLibraryAndVariesPerRow()
    {
        // A two-package family: a built-in (framework-only) row with no min-agent, and a
        // NuGet row carrying its own per-tab min-agent (core 10.35.0, framework 10.36.0).
        // The package-level value must override the absent library-level one per row.
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
                            Name = "System.Data.ODBC",
                            Packages =
                            {
                                new Package
                                {
                                    Id = "System.Data", Tabs = { "framework" }, VersionSource = "manual",
                                    MinVersion = VersionSpec.Single("4.6.2"), LatestVersion = VersionSpec.Single("4.8")
                                },
                                new Package
                                {
                                    Id = "System.Data.Odbc", Tabs = { "core", "framework" },
                                    MinVersion = VersionSpec.Single("8.0.0"),
                                    MinAgentVersion = VersionSpec.Map(new Dictionary<string, string>
                                        { ["core"] = "10.35.0", ["framework"] = "10.36.0" })
                                }
                            }
                        }
                    }
                }
            }
        };

        var versions = new Dictionary<(string, Platform), string>
        {
            { ("system.data.odbc", Platform.Core), "10.0.8" },
            { ("system.data.odbc", Platform.Framework), "10.0.8" },
        };

        var md = new MarkdownRenderer(new NoteRenderer()).Render(model, versions).Replace("\r\n", "\n");

        var coreSection = md[md.IndexOf("## .NET Core", StringComparison.Ordinal)..md.IndexOf("## .NET Framework", StringComparison.Ordinal)];
        var fwSection = md[md.IndexOf("## .NET Framework", StringComparison.Ordinal)..];

        // Core: only the NuGet package row, min-agent 10.35.0.
        Assert.That(coreSection, Does.Contain("| System.Data.ODBC | System.Data.Odbc | 8.0.0 – 10.0.8 | 10.35.0 |"));
        // Framework: built-in row has no min-agent; NuGet row has 10.36.0.
        Assert.That(fwSection, Does.Contain("| System.Data.ODBC | System.Data | 4.6.2 – 4.8 | — |"));
        Assert.That(fwSection, Does.Contain("| System.Data.ODBC | System.Data.Odbc | 8.0.0 – 10.0.8 | 10.36.0 |"));
    }
}
