namespace Glyph.Infrastructure.Forms;

/// <summary>
/// User-defined contact profile for optional AcroForm AutoFill.
/// </summary>
public sealed record FormAutofillProfile(
    string Name = "",
    string Address = "",
    string Email = "",
    string Phone = "")
{
    public bool HasAnyValue =>
        !string.IsNullOrWhiteSpace(Name)
        || !string.IsNullOrWhiteSpace(Address)
        || !string.IsNullOrWhiteSpace(Email)
        || !string.IsNullOrWhiteSpace(Phone);
}
