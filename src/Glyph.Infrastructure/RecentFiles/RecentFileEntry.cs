using Glyph.Core.Documents;

namespace Glyph.Infrastructure.RecentFiles;

public sealed record RecentFileEntry(
    string Path,
    DocumentKind Kind,
    DateTimeOffset LastOpenedUtc,
    string DisplayName);
