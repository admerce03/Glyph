namespace Glyph.Pdf.Abstractions;

/// <summary>Outcome of a password / permission write attempt.</summary>
public sealed record PdfSecurityWriteResult(bool Succeeded, string Message)
{
    public static PdfSecurityWriteResult Ok(string message = "OK") => new(true, message);

    public static PdfSecurityWriteResult Failed(string message) => new(false, message);
}
