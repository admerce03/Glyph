namespace Glyph.Core.Signatures;

/// <summary>
/// Display and accessibility strings for signature library entries (F19).
/// </summary>
public static class SignatureDisplayText
{
    public static string ListLabel(SignatureEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return string.IsNullOrWhiteSpace(entry.Description)
            ? entry.Name
            : $"{entry.Name} — {entry.Description}";
    }

    public static string Contents(SignatureEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return Contents(entry.Name, entry.Description);
    }

    public static string Contents(string name, string? description)
        => string.IsNullOrWhiteSpace(description)
            ? $"Signature: {name}"
            : description.Trim();
}
