namespace Glyph.Core.IO;

/// <summary>
/// Same-name move into a picked folder (F01-20).
/// </summary>
public static class DocumentMovePolicy
{
    public static string DestinationPath(string sourcePath, string destinationFolder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationFolder);
        return Path.Combine(destinationFolder, Path.GetFileName(sourcePath));
    }

    public static bool IsSameFolder(string sourcePath, string destinationFolder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationFolder);

        try
        {
            var sourceDir = Path.GetDirectoryName(sourcePath) ?? string.Empty;
            return string.Equals(
                Path.GetFullPath(sourceDir),
                Path.GetFullPath(destinationFolder),
                StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    public static MoveOverwriteDecision EvaluateOverwrite(bool destinationExists, bool destinationIsReadOnly, bool userConfirmedReplace)
    {
        if (!destinationExists)
        {
            return MoveOverwriteDecision.Proceed;
        }

        if (!userConfirmedReplace)
        {
            return MoveOverwriteDecision.Cancelled;
        }

        return destinationIsReadOnly
            ? MoveOverwriteDecision.BlockedReadOnly
            : MoveOverwriteDecision.Proceed;
    }
}

public enum MoveOverwriteDecision
{
    Proceed,
    Cancelled,
    BlockedReadOnly,
}
