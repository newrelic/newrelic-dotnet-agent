using CompatibilityDocs.Rendering;
using CompatibilityDocs.Schema;
using NUnit.Framework;

namespace CompatibilityDocs.Tests;

[TestFixture]
public class NoteRendererTests
{
    private readonly NoteRenderer _renderer = new();

    [Test]
    public void Render_AddedInAgent()
    {
        var s = _renderer.Render(new Note { Type = "addedInAgent", SinceVersion = "3.35.0", AgentVersion = "10.32.0" });
        Assert.That(s, Is.EqualTo("Versions 3.35.0+ supported since agent v10.32.0."));
    }

    [Test]
    public void Render_MaxSupportedVersion()
    {
        var s = _renderer.Render(new Note { Type = "maxSupportedVersion", Version = "8.15.10" });
        Assert.That(s, Is.EqualTo("Maximum supported version: 8.15.10."));
    }

    [Test]
    public void Render_KnownIncompatibleVersions_FromList()
    {
        var s = _renderer.Render(new Note { Type = "knownIncompatibleVersions", Versions = new() { "3.0.x", "3.1.x" } });
        Assert.That(s, Is.EqualTo("Known incompatible versions: 3.0.x, 3.1.x."));
    }

    [Test]
    public void Render_KnownIncompatibleVersions_FromText()
    {
        var s = _renderer.Render(new Note { Type = "knownIncompatibleVersions", Text = "4.0.44 or higher" });
        Assert.That(s, Is.EqualTo("Known incompatible versions: 4.0.44 or higher."));
    }

    [Test]
    public void Render_RequiresHybridAgent_AboveVersion()
    {
        var s = _renderer.Render(new Note { Type = "requiresHybridAgent", AboveVersion = "8.15.10" });
        Assert.That(s, Is.EqualTo(
            "Versions later than 8.15.10 are supported only when [OpenTelemetry API support](https://docs.newrelic.com/docs/apm/agents/manage-apm-agents/opentelemetry-api-support/) is enabled."));
    }

    [Test]
    public void Render_RequiresHybridAgent_WholeLibrary()
    {
        var s = _renderer.Render(new Note { Type = "requiresHybridAgent" });
        Assert.That(s, Is.EqualTo(
            "Supported only when [OpenTelemetry API support](https://docs.newrelic.com/docs/apm/agents/manage-apm-agents/opentelemetry-api-support/) is enabled."));
    }

    [Test]
    public void Render_Freeform_Verbatim()
    {
        var s = _renderer.Render(new Note { Type = "freeform", Text = "Prior versions may be instrumented." });
        Assert.That(s, Is.EqualTo("Prior versions may be instrumented."));
    }
}
