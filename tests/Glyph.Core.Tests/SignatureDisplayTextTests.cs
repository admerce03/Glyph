using FluentAssertions;
using Glyph.Core.Signatures;

namespace Glyph.Core.Tests;

public class SignatureDisplayTextTests
{
    private static SignatureEntry Entry(string name, string description = "") =>
        new("id", name, "file.png", DateTimeOffset.UnixEpoch, description);

    [Fact]
    public void ListLabel_name_only_when_description_blank()
    {
        SignatureDisplayText.ListLabel(Entry("Ada")).Should().Be("Ada");
        SignatureDisplayText.ListLabel(Entry("Ada", "  ")).Should().Be("Ada");
    }

    [Fact]
    public void ListLabel_includes_description()
    {
        SignatureDisplayText.ListLabel(Entry("Ada", "Initials")).Should().Be("Ada — Initials");
    }

    [Fact]
    public void Contents_falls_back_to_signature_name()
    {
        SignatureDisplayText.Contents(Entry("Ada")).Should().Be("Signature: Ada");
        SignatureDisplayText.Contents("Ada", null).Should().Be("Signature: Ada");
        SignatureDisplayText.Contents("Ada", "  ").Should().Be("Signature: Ada");
    }

    [Fact]
    public void Contents_uses_trimmed_description()
    {
        SignatureDisplayText.Contents(Entry("Ada", "  Initials  ")).Should().Be("Initials");
        SignatureDisplayText.Contents("Ada", "  Initials  ").Should().Be("Initials");
    }
}
