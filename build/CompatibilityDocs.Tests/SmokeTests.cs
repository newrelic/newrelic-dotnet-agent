using NUnit.Framework;

namespace CompatibilityDocs.Tests;

[TestFixture]
public class SmokeTests
{
    [Test]
    public void ToolNamespace_IsReachable()
    {
        Assert.That(typeof(Program).Namespace, Is.EqualTo("CompatibilityDocs"));
    }
}
