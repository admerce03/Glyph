namespace Glyph.Core.Pdf;

/// <summary>
/// Chooses which <c>IPdfSecurityService</c> write path the Protect dialog should invoke.
/// </summary>
public static class PdfSecurityWriteApplyPolicy
{
    public enum Kind
    {
        SetOpenPassword,
        SetPermissions,
        Invalid,
    }

    public sealed record Decision(
        Kind Kind,
        string? UserPassword,
        string? OwnerPassword,
        string? FailureMessage);

    public static Decision Decide(string? openPassword, string? ownerPassword)
    {
        var open = openPassword ?? string.Empty;
        var owner = ownerPassword ?? string.Empty;

        if (!string.IsNullOrEmpty(open))
        {
            return new Decision(
                Kind.SetOpenPassword,
                UserPassword: open,
                OwnerPassword: string.IsNullOrEmpty(owner) ? null : owner,
                FailureMessage: null);
        }

        if (!string.IsNullOrEmpty(owner))
        {
            return new Decision(
                Kind.SetPermissions,
                UserPassword: null,
                OwnerPassword: owner,
                FailureMessage: null);
        }

        return new Decision(
            Kind.Invalid,
            UserPassword: null,
            OwnerPassword: null,
            FailureMessage: "Enter an open password and/or an owner password.");
    }
}
