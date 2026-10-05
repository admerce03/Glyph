namespace Glyph.Core.Documents;

/// <summary>
/// Form text-field multiline editing (F20-02).
/// </summary>
public static class FormMultilinePolicy
{
    public static bool AcceptsReturnFor(string fieldKindName) =>
        fieldKindName.Contains("Text", StringComparison.OrdinalIgnoreCase);

    public const bool TextFieldAcceptsReturn = true;
}
