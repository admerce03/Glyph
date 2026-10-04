namespace Glyph.Core.Documents;

/// <summary>
/// Stable identity for an open document session within the running app.
/// </summary>
public readonly record struct DocumentId(Guid Value)
{
    public static DocumentId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("N");
}
