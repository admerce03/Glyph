namespace Glyph.Core.Documents;

/// <summary>
/// Cold-start ready status line (F57-01).
/// </summary>
public static class StartupReadyStatus
{
    public static string Format(long elapsedMilliseconds) =>
        $"Ready — started in {elapsedMilliseconds} ms. File → Open or drop files here";
}
