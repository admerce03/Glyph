namespace Glyph.Ocr.Abstractions;

/// <summary>
/// Launch / clipboard targets for OCR entity actions (F08-18–24).
/// </summary>
public static class OcrEntityActionUris
{
    public static string NormalizeUrl(string value) =>
        value.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? value
            : "https://" + value;

    public static string Mailto(string email) => "mailto:" + email;

    public static string BingMaps(string address) =>
        "https://www.bing.com/maps?q=" + Uri.EscapeDataString(address);

    public static string BingWebSearch(string query) =>
        "https://www.bing.com/search?q=" + Uri.EscapeDataString(query.Trim());

    public const string OpenedUrl = "Opened URL.";
    public const string OpenedMail = "Opened mail compose.";
    public const string OpenedMaps = "Opened address in Maps.";
    public const string OpenedWebSearch = "Opened web search.";
    public const string NothingToSearch = "Nothing to search.";
    public const string ActionFailedPrefix = "Entity action failed: ";

    public static string CopiedKind(string kind) => $"Copied {kind}.";

    public static string ActionFailed(string message) => ActionFailedPrefix + message;

    public static string SearchFailed(string message) => "Search web failed: " + message;
}
