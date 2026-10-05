namespace Glyph.Core.Documents;

/// <summary>
/// External URL / maps launch helpers (F47-03 / F47-04).
/// </summary>
public static class ExternalLaunchPolicy
{
    public static bool LooksLikeHttpUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return value.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
    }

    public static string NormalizeHttpUrl(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (LooksLikeHttpUrl(value))
        {
            return value.Trim();
        }

        return "https://" + value.Trim();
    }
}
