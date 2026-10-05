using System.Xml.Linq;

namespace Glyph.Core.Documents;

/// <summary>
/// Declared MSIX file-type associations in <c>Package.appxmanifest</c> (ADR-012 / F01-06/07).
/// Unit tests assert the manifest lists these extensions; Explorer registration stays
/// Deferred until <c>install-msix-test.ps1</c> sideload is verified on Windows.
/// </summary>
public static class PackageFileAssociationDeclaration
{
    public const string ManifestRelativePath = PackagingDeferredPolicy.ManifestPath;

    public const string Publisher = "CN=Glyph";
    public const string IdentityName = "Glyph.Desktop";

    /// <summary>Extensions Glyph registers via MSIX (lowercase, with leading dot).</summary>
    public static readonly string[] ExpectedExtensions =
    [
        ".pdf",
        ".png",
        ".jpg",
        ".jpeg",
        ".gif",
        ".bmp",
        ".tif",
        ".tiff",
        ".webp",
    ];

    public static IReadOnlyList<string> ParseDeclaredExtensions(string manifestXml)
    {
        if (string.IsNullOrWhiteSpace(manifestXml))
        {
            return Array.Empty<string>();
        }

        var doc = XDocument.Parse(manifestXml);
        XNamespace uap = "http://schemas.microsoft.com/appx/manifest/uap/windows10";
        return doc.Descendants(uap + "FileType")
            .Select(e => (e.Value ?? string.Empty).Trim().ToLowerInvariant())
            .Where(v => v.StartsWith('.'))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(v => v, StringComparer.Ordinal)
            .ToArray();
    }

    public static bool ManifestDeclaresExpectedAssociations(string manifestXml)
    {
        var declared = ParseDeclaredExtensions(manifestXml);
        if (declared.Count == 0)
        {
            return false;
        }

        foreach (var ext in ExpectedExtensions)
        {
            if (!declared.Contains(ext, StringComparer.Ordinal))
            {
                return false;
            }
        }

        return ManifestHasIdentity(manifestXml);
    }

    public static bool ManifestHasIdentity(string manifestXml)
    {
        if (string.IsNullOrWhiteSpace(manifestXml))
        {
            return false;
        }

        var doc = XDocument.Parse(manifestXml);
        XNamespace ns = "http://schemas.microsoft.com/appx/manifest/foundation/windows10";
        var identity = doc.Root?.Element(ns + "Identity");
        if (identity is null)
        {
            return false;
        }

        return string.Equals((string?)identity.Attribute("Name"), IdentityName, StringComparison.Ordinal)
            && string.Equals((string?)identity.Attribute("Publisher"), Publisher, StringComparison.Ordinal);
    }
}
