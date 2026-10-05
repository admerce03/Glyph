namespace Glyph.Core.Documents;

/// <summary>
/// Deferred full-document search indexing (F57-05 / F58-06).
/// Search remains on-demand.
/// </summary>
public static class BackgroundSearchIndexPolicy
{
    public const bool BackgroundIndexingEnabled = false;
    public const bool SearchIsOnDemand = true;

    public const string DeferredReason =
        "Full-document background indexing is not required yet; Find runs on demand.";
}
