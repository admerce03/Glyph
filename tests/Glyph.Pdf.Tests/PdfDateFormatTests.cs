using FluentAssertions;
using Glyph.Pdf.Abstractions;

namespace Glyph.Pdf.Tests;

public class PdfDateFormatTests
{
    [Fact]
    public void Formats_pdf_date_prefix()
    {
        var formatted = PdfDateFormat.Format(new DateTimeOffset(2026, 10, 5, 14, 30, 0, TimeSpan.Zero));
        formatted.Should().StartWith("D:20261005143000");
    }
}
