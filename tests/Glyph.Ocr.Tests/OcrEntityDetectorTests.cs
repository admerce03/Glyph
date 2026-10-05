using FluentAssertions;
using Glyph.Ocr.Abstractions;
using Xunit;

namespace Glyph.Ocr.Tests;

public class OcrEntityDetectorTests
{
    [Fact]
    public void Detect_finds_url_email_phone_date_and_time()
    {
        var text = "Visit https://glyph.example/docs or mail team@glyph.example. Call +1 (415) 555-0100 on Oct 5, 2026 at 14:30.";
        var entities = OcrEntityDetector.Detect(text);

        entities.Should().Contain(e => e.Kind == OcrEntityKind.Url && e.Value.Contains("glyph.example"));
        entities.Should().Contain(e => e.Kind == OcrEntityKind.Email && e.Value == "team@glyph.example");
        entities.Should().Contain(e => e.Kind == OcrEntityKind.Phone && e.Value.Contains("415"));
        entities.Should().Contain(e => e.Kind == OcrEntityKind.Date && e.Value.Contains("Oct"));
        entities.Should().Contain(e => e.Kind == OcrEntityKind.Time && e.Value.StartsWith("14:30"));
    }

    [Fact]
    public void Detect_returns_empty_for_blank_text()
    {
        OcrEntityDetector.Detect(null).Should().BeEmpty();
        OcrEntityDetector.Detect("   ").Should().BeEmpty();
        OcrEntityDetector.Detect("no entities here").Should().BeEmpty();
    }

    [Fact]
    public void Detect_www_url_without_scheme()
    {
        var entities = OcrEntityDetector.Detect("See www.example.com/path for details.");
        entities.Should().ContainSingle(e => e.Kind == OcrEntityKind.Url && e.Value.StartsWith("www.example.com"));
    }

    [Fact]
    public void Detect_finds_street_address_with_city_state_zip()
    {
        var text = "Ship to 1600 Amphitheatre Parkway, Mountain View, CA 94043 today.";
        var entities = OcrEntityDetector.Detect(text);

        entities.Should().Contain(e =>
            e.Kind == OcrEntityKind.Address
            && e.Value.Contains("1600 Amphitheatre Parkway", StringComparison.OrdinalIgnoreCase)
            && e.Value.Contains("Mountain View, CA 94043", StringComparison.Ordinal));
    }

    [Fact]
    public void Detect_finds_standalone_city_state_zip()
    {
        var entities = OcrEntityDetector.Detect("Office in Austin, TX 78701 near downtown.");
        entities.Should().Contain(e => e.Kind == OcrEntityKind.Address && e.Value == "Austin, TX 78701");
    }

    [Fact]
    public void Detect_finds_street_with_suite()
    {
        var entities = OcrEntityDetector.Detect("Meet at 500 Market Street Suite 200 for lunch.");
        entities.Should().Contain(e =>
            e.Kind == OcrEntityKind.Address
            && e.Value.Contains("500 Market Street", StringComparison.OrdinalIgnoreCase)
            && e.Value.Contains("Suite 200", StringComparison.OrdinalIgnoreCase));
    }
}

public class OcrCalendarInviteTests
{
    [Fact]
    public void BuildAllDayEvent_contains_date_fields()
    {
        var ics = OcrCalendarInvite.BuildAllDayEvent(new DateTime(2026, 10, 5), "Meeting");
        ics.Should().Contain("BEGIN:VEVENT");
        ics.Should().Contain("DTSTART;VALUE=DATE:20261005");
        ics.Should().Contain("DTEND;VALUE=DATE:20261006");
        ics.Should().Contain("SUMMARY:Meeting");
        ics.Should().Contain("END:VCALENDAR");
    }

    [Fact]
    public void BuildTimedEvent_contains_local_start_end()
    {
        var start = new DateTime(2026, 10, 5, 14, 30, 0);
        var ics = OcrCalendarInvite.BuildTimedEvent(start, TimeSpan.FromHours(1), "Call");
        ics.Should().Contain("DTSTART:20261005T143000");
        ics.Should().Contain("DTEND:20261005T153000");
        ics.Should().Contain("SUMMARY:Call");
    }

    [Fact]
    public void TryParseDate_and_time()
    {
        OcrCalendarInvite.TryParseDate("Oct 5, 2026", out var date).Should().BeTrue();
        date.Date.Should().Be(new DateTime(2026, 10, 5));

        OcrCalendarInvite.TryParseTime("14:30", out var time).Should().BeTrue();
        time.Should().Be(new TimeSpan(14, 30, 0));
    }
}
