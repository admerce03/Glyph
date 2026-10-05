using FluentAssertions;
using Glyph.Core.Signatures;

namespace Glyph.Core.Tests;

public class SignatureFormPlacementPolicyTests
{
    [Fact]
    public void Visual_stamp_not_pkcs7()
    {
        SignatureFormPlacementPolicy.PlacesVisualStampInFieldBounds.Should().BeTrue();
        SignatureFormPlacementPolicy.CryptographicPkcs7Supported.Should().BeFalse();
        SignatureFormPlacementPolicy.IsSignatureWidget("Signature").Should().BeTrue();
        SignatureFormPlacementPolicy.IsSignatureWidget("Text").Should().BeFalse();
    }
}

public class SignatureLibraryUiTests
{
    [Fact]
    public void Library_capabilities()
    {
        SignatureLibraryUi.ImageMarkupUsesPasteFile.Should().BeTrue();
        SignatureLibraryUi.SupportsSaveDeleteReorderDescriptions.Should().BeTrue();
        SignatureLibraryUi.LibraryTitle.Should().Contain("Signature");
        SignatureLibraryUi.DrawCancelled.Should().Contain("cancelled");
        SignatureLibraryUi.Inserting.Should().Contain("Inserting");
        SignatureLibraryUi.FormatDeleted("A").Should().Contain("A");
        SignatureLibraryUi.FormatInserted("B").Should().Contain("B");
        SignatureLibraryUi.SigningFormField.Should().Contain("Signing");
    }
}
