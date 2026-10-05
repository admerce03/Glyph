namespace Glyph.Core.Documents;

/// <summary>
/// Manual “Check for updates” (F55-04). Opens the GitHub Releases page; does not
/// auto-download or silent-update. Keep <see cref="ShippedVersion"/> aligned with
/// <c>Package.appxmanifest</c> Identity/@Version (major.minor.patch).
/// </summary>
public static class AppUpdateCheckPolicy
{
    public const string ReleasesUrl = "https://github.com/admerce03/Glyph/releases";
    public const string LatestReleaseApiUrl =
        "https://api.github.com/repos/admerce03/Glyph/releases/latest";

    /// <summary>Marketing / compare version (no fourth .0 build component).</summary>
    public const string ShippedVersion = "0.1.0";

    public const string DialogTitle = "Check for updates";
    public const string OpenReleasesButton = "Open releases";
    public const string CloseButton = "Close";

    public static string FormatCurrentOnly() =>
        $"Glyph {ShippedVersion}{Environment.NewLine}{Environment.NewLine}" +
        "Open the releases page to see whether a newer build is available.";

    public static string FormatWithLatest(string? latestTag)
    {
        var latest = NormalizeVersion(latestTag);
        if (string.IsNullOrEmpty(latest))
        {
            return FormatCurrentOnly();
        }

        var cmp = CompareSemVer(ShippedVersion, latest);
        if (cmp < 0)
        {
            return $"Glyph {ShippedVersion}{Environment.NewLine}{Environment.NewLine}" +
                   $"A newer release is available: {latest}.";
        }

        if (cmp > 0)
        {
            return $"Glyph {ShippedVersion}{Environment.NewLine}{Environment.NewLine}" +
                   $"You are ahead of the latest published release ({latest}).";
        }

        return $"Glyph {ShippedVersion}{Environment.NewLine}{Environment.NewLine}" +
               "You are on the latest published release.";
    }

    public static string NormalizeVersion(string? tagOrVersion)
    {
        if (string.IsNullOrWhiteSpace(tagOrVersion))
        {
            return string.Empty;
        }

        var s = tagOrVersion.Trim();
        if (s.StartsWith('v') || s.StartsWith('V'))
        {
            s = s[1..];
        }

        var cut = s.IndexOfAny(['-', '+']);
        if (cut >= 0)
        {
            s = s[..cut];
        }

        return s;
    }

    /// <summary>Negative if <paramref name="a"/> is older than <paramref name="b"/>.</summary>
    public static int CompareSemVer(string? a, string? b)
    {
        var pa = ParseParts(NormalizeVersion(a));
        var pb = ParseParts(NormalizeVersion(b));
        for (var i = 0; i < 3; i++)
        {
            var c = pa[i].CompareTo(pb[i]);
            if (c != 0)
            {
                return c;
            }
        }

        return 0;
    }

    private static int[] ParseParts(string version)
    {
        var parts = new int[3];
        if (string.IsNullOrEmpty(version))
        {
            return parts;
        }

        var bits = version.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        for (var i = 0; i < Math.Min(3, bits.Length); i++)
        {
            if (int.TryParse(bits[i], out var n) && n >= 0)
            {
                parts[i] = n;
            }
        }

        return parts;
    }
}
