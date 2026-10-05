namespace Glyph.Core.Signatures;

public sealed record SignatureEntry(
    string Id,
    string Name,
    string FileName,
    DateTimeOffset CreatedUtc);
