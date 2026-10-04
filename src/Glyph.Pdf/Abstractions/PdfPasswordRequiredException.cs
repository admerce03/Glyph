namespace Glyph.Pdf.Abstractions;

/// <summary>
/// Thrown when a PDF requires a password (or the supplied password was rejected).
/// </summary>
public sealed class PdfPasswordRequiredException : Exception
{
    public PdfPasswordRequiredException(string path, bool passwordWasProvided)
        : base(
            passwordWasProvided
                ? $"Incorrect password for PDF: {path}"
                : $"Password required to open PDF: {path}")
    {
        Path = path;
        PasswordWasProvided = passwordWasProvided;
    }

    public string Path { get; }

    public bool PasswordWasProvided { get; }
}
