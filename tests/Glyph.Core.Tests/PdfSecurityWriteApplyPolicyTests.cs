using FluentAssertions;
using Glyph.Core.Pdf;

namespace Glyph.Core.Tests;

public class PdfSecurityWriteApplyPolicyTests
{
    [Fact]
    public void Open_password_selects_set_open()
    {
        var d = PdfSecurityWriteApplyPolicy.Decide("secret", ownerPassword: null);
        d.Kind.Should().Be(PdfSecurityWriteApplyPolicy.Kind.SetOpenPassword);
        d.UserPassword.Should().Be("secret");
        d.OwnerPassword.Should().BeNull();
    }

    [Fact]
    public void Open_plus_owner_keeps_owner()
    {
        var d = PdfSecurityWriteApplyPolicy.Decide("secret", "owner");
        d.Kind.Should().Be(PdfSecurityWriteApplyPolicy.Kind.SetOpenPassword);
        d.OwnerPassword.Should().Be("owner");
    }

    [Fact]
    public void Owner_only_selects_set_permissions()
    {
        var d = PdfSecurityWriteApplyPolicy.Decide(openPassword: "", ownerPassword: "owner");
        d.Kind.Should().Be(PdfSecurityWriteApplyPolicy.Kind.SetPermissions);
        d.OwnerPassword.Should().Be("owner");
        d.UserPassword.Should().BeNull();
    }

    [Fact]
    public void Empty_both_is_invalid()
    {
        var d = PdfSecurityWriteApplyPolicy.Decide("", "");
        d.Kind.Should().Be(PdfSecurityWriteApplyPolicy.Kind.Invalid);
        d.FailureMessage.Should().Contain("open password");
    }
}
