using System.Text;
using FluentAssertions;
using Glyph.Pdf.Pdfium;
using Xunit;

namespace Glyph.Pdf.Tests;

public class PdfInfoDictionaryPatcherTests
{
    [Fact]
    public void Encode_ascii_literal()
    {
        PdfInfoDictionaryPatcher.Encode("Glyph").Should().Be("(Glyph)");
    }

    [Fact]
    public void Encode_escapes_parens_via_unicode_hex()
    {
        var encoded = PdfInfoDictionaryPatcher.Encode("a(b)c");
        encoded.Should().StartWith("<FEFF");
        encoded.Should().EndWith(">");
        encoded.Should().NotContain("(");
    }

    [Fact]
    public void Encode_non_ascii_uses_feff_utf16be()
    {
        var encoded = PdfInfoDictionaryPatcher.Encode("café");
        encoded.Should().StartWith("<FEFF");
        // UTF-16BE for "café": 0063 0061 0066 00E9
        encoded.Should().Contain("00630061006600E9");
        encoded.Should().EndWith(">");
    }

    [Fact]
    public void BuildInfoDictionary_omits_null_includes_empty()
    {
        var dict = PdfInfoDictionaryPatcher.BuildInfoDictionary(
            new PdfInfoFields(
                Title: "T",
                Author: null,
                Subject: "",
                Keywords: null,
                Creator: "C",
                Producer: null,
                CreationDate: null,
                ModDate: "D:20260101120000Z"));

        dict.Should().StartWith("<<");
        dict.Should().Contain("/Title (T)");
        dict.Should().Contain("/Subject ()");
        dict.Should().Contain("/Creator (C)");
        dict.Should().Contain("/ModDate (D:20260101120000Z)");
        dict.Should().NotContain("/Author");
        dict.Should().NotContain("/Keywords");
        dict.Should().NotContain("/Producer");
        dict.Should().NotContain("/CreationDate");
        dict.Should().EndWith(" >>");
    }

    [Fact]
    public void Apply_appends_info_xref_trailer_with_prev_and_id()
    {
        var original = Encoding.Latin1.GetBytes(
            "%PDF-1.4\n" +
            "1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n" +
            "2 0 obj\n<< /Type /Pages /Kids [] /Count 0 >>\nendobj\n" +
            "xref\n0 3\n0000000000 65535 f \n0000000009 00000 n \n0000000068 00000 n \n" +
            "trailer\n<< /Size 3 /Root 1 0 R /ID [<AABB> <CCDD>] >>\n" +
            "startxref\n120\n%%EOF\n");

        var patched = PdfInfoDictionaryPatcher.Apply(
            original,
            new PdfInfoFields("Title", null, null, null, Creator: "Glyph"));

        var text = Encoding.Latin1.GetString(patched);
        text.Should().Contain("/Title (Title)");
        text.Should().Contain("/Creator (Glyph)");
        text.Should().Contain("/Info ");
        text.Should().Contain("/Prev 120");
        text.Should().Contain("/ID [<AABB> <CCDD>]");
        text.Should().EndWith("%%EOF\n");
        // Incremental update keeps the original bytes as a prefix (minus trailing EOF).
        text.Should().StartWith("%PDF-1.4\n");
    }

    [Fact]
    public void Apply_rejects_truncated_buffer()
    {
        var act = () => PdfInfoDictionaryPatcher.Apply([1, 2, 3], new PdfInfoFields("T", null, null, null));
        act.Should().Throw<InvalidOperationException>().WithMessage("*too small*");
    }

    [Fact]
    public void Apply_rejects_missing_startxref()
    {
        var bytes = Encoding.Latin1.GetBytes("%PDF-1.4\ntrailer\n<< /Size 1 /Root 1 0 R >>\n");
        var act = () => PdfInfoDictionaryPatcher.Apply(bytes, new PdfInfoFields("T", null, null, null));
        act.Should().Throw<InvalidOperationException>().WithMessage("*startxref*");
    }
}
