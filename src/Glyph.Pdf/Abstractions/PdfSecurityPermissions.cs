namespace Glyph.Pdf.Abstractions;

/// <summary>
/// PDF Standard Security Handler permission flags (F23-04/05). Defaults allow all operations.
/// </summary>
public sealed record PdfSecurityPermissions(
    bool PermitPrint = true,
    bool PermitFullQualityPrint = true,
    bool PermitModifyDocument = true,
    bool PermitExtractContent = true,
    bool PermitAnnotations = true,
    bool PermitFormsFill = true,
    bool PermitAssembleDocument = true)
{
    public static PdfSecurityPermissions AllowAll { get; } = new();

    public static PdfSecurityPermissions RestrictAll { get; } = new(
        PermitPrint: false,
        PermitFullQualityPrint: false,
        PermitModifyDocument: false,
        PermitExtractContent: false,
        PermitAnnotations: false,
        PermitFormsFill: false,
        PermitAssembleDocument: false);
}
