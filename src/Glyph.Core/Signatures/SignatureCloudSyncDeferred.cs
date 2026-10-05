namespace Glyph.Core.Signatures;

/// <summary>
/// Optional cloud sync for signatures deferred (F19-09).
/// </summary>
public static class SignatureCloudSyncDeferred
{
    public const bool ApplicationSpecificCloudSyncSupported = false;

    public const string Reason =
        "Explicitly later; local FileSignatureLibrary is the shipping path.";
}
