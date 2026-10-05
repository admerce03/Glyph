namespace Glyph.Core.Documents;

/// <summary>
/// Third-party notices shipped with the app (AGENTS.md / ADR-003 redistributables).
/// </summary>
public static class ThirdPartyNoticesPolicy
{
    public const string RepoRelativePath = "THIRD_PARTY_NOTICES.md";

    public const string AboutButton = "Third-party notices";

    public const string DialogTitle = "Third-party notices";

    public const string MissingFile =
        "THIRD_PARTY_NOTICES.md was not found next to the application. See the Glyph repository root.";

    public const bool ShippedInRepo = true;
}
