namespace Glyph.Infrastructure.Forms;

/// <summary>
/// Remembers recently typed AcroForm text values (per field name and global).
/// </summary>
public interface IFormValueHistory
{
    /// <summary>
    /// Recent values for a field name (most recent first), falling back to global history.
    /// </summary>
    IReadOnlyList<string> GetSuggestions(string? fieldName, int limit = 8);

    Task RememberAsync(string? fieldName, string value, CancellationToken cancellationToken = default);

    Task ClearAsync(CancellationToken cancellationToken = default);
}
