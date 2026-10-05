using FluentAssertions;
using Glyph.Pdf.Pdfium;

namespace Glyph.Pdf.Tests;

public class PdfFreeTextQuaddingPatcherTests
{
    [Fact]
    public void Apply_inserts_q_before_subtype()
    {
        var pdf = """
            %PDF-1.4
            1 0 obj
            <</Type/Page/Annots[<</NM(GlyphQabc)/Contents(Hi)/Subtype/FreeText/Type/Annot>>]>>
            endobj
            trailer<</Size 2>>
            startxref
            0
            %%EOF
            """u8.ToArray();

        var patched = PdfFreeTextQuaddingPatcher.Apply(pdf, "GlyphQabc", 1);
        var text = System.Text.Encoding.Latin1.GetString(patched);
        text.Should().Contain("/Q 1/Subtype/FreeText");
        text.Should().Contain("/NM(GlyphQabc)");
    }

    [Fact]
    public void Apply_replaces_existing_q()
    {
        var pdf = """
            %PDF-1.4
            1 0 obj
            <</Type/Page/Annots[<</NM(GlyphQxyz)/Q 0/Contents(Hi)/Subtype/FreeText/Type/Annot>>]>>
            endobj
            trailer<</Size 2>>
            startxref
            0
            %%EOF
            """u8.ToArray();

        var patched = PdfFreeTextQuaddingPatcher.Apply(pdf, "GlyphQxyz", 2);
        var text = System.Text.Encoding.Latin1.GetString(patched);
        text.Should().Contain("/Q 2");
        text.Should().NotContain("/Q 0");
    }
}
