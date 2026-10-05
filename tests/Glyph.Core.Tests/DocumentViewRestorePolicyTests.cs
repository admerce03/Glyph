using FluentAssertions;
using Glyph.Core.Documents;

namespace Glyph.Core.Tests;

public class DocumentViewRestorePolicyTests
{
    [Theory]
    [InlineData(null, true)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void Effective_remember_flags_default_on(bool? stored, bool expected)
    {
        DocumentViewRestorePolicy.EffectiveRememberLastPage(stored).Should().Be(expected);
        DocumentViewRestorePolicy.EffectiveRememberZoom(stored).Should().Be(expected);
    }

    [Fact]
    public void ApplyPdf_without_saved_uses_defaults()
    {
        var target = new DocumentViewState { Zoom = 9, CurrentPageIndex = 4 };
        DocumentViewRestorePolicy.ApplyPdf(
            target,
            saved: null,
            pageCount: 10,
            rememberLastPage: true,
            rememberZoom: true,
            defaultZoom: 1.5,
            defaultLayout: PageLayoutMode.TwoPage);

        target.Zoom.Should().Be(1.5);
        target.PageLayout.Should().Be(PageLayoutMode.TwoPage);
        target.CurrentPageIndex.Should().Be(0);
    }

    [Fact]
    public void ApplyPdf_remembers_page_and_zoom_when_enabled()
    {
        var target = new DocumentViewState();
        var saved = new DocumentViewState
        {
            Zoom = 2.0,
            CurrentPageIndex = 7,
            PageLayout = PageLayoutMode.SinglePage,
        };

        DocumentViewRestorePolicy.ApplyPdf(
            target,
            saved,
            pageCount: 20,
            rememberLastPage: true,
            rememberZoom: true,
            defaultZoom: 1.25,
            defaultLayout: PageLayoutMode.Continuous);

        target.Zoom.Should().Be(2.0);
        target.CurrentPageIndex.Should().Be(7);
        target.PageLayout.Should().Be(PageLayoutMode.SinglePage);
    }

    [Fact]
    public void ApplyPdf_skips_page_and_zoom_when_disabled()
    {
        var target = new DocumentViewState();
        var saved = new DocumentViewState
        {
            Zoom = 2.0,
            CurrentPageIndex = 7,
            PageLayout = PageLayoutMode.SinglePage,
        };

        DocumentViewRestorePolicy.ApplyPdf(
            target,
            saved,
            pageCount: 20,
            rememberLastPage: false,
            rememberZoom: false,
            defaultZoom: 1.5,
            defaultLayout: PageLayoutMode.Continuous);

        target.Zoom.Should().Be(1.5);
        target.CurrentPageIndex.Should().Be(0);
        target.PageLayout.Should().Be(PageLayoutMode.SinglePage);
    }

    [Fact]
    public void ApplyPdf_clamps_remembered_page_to_document()
    {
        var target = new DocumentViewState();
        var saved = new DocumentViewState { CurrentPageIndex = 99, Zoom = 1.1 };

        DocumentViewRestorePolicy.ApplyPdf(
            target,
            saved,
            pageCount: 5,
            rememberLastPage: true,
            rememberZoom: true,
            defaultZoom: 1.25,
            defaultLayout: PageLayoutMode.Continuous);

        target.CurrentPageIndex.Should().Be(4);
    }

    [Fact]
    public void ApplyImage_restores_zoom_only_when_enabled()
    {
        var on = new DocumentViewState { Zoom = 1.0 };
        DocumentViewRestorePolicy.ApplyImage(
            on,
            new DocumentViewState { Zoom = 3.0 },
            rememberZoom: true);
        on.Zoom.Should().Be(3.0);

        var off = new DocumentViewState { Zoom = 1.0 };
        DocumentViewRestorePolicy.ApplyImage(
            off,
            new DocumentViewState { Zoom = 3.0 },
            rememberZoom: false);
        off.Zoom.Should().Be(1.0);
    }

    [Fact]
    public void ResolveDefaultPageLayout_maps_setting_names()
    {
        DocumentViewRestorePolicy.ResolveDefaultPageLayout("Single")
            .Should().Be(PageLayoutMode.SinglePage);
        DocumentViewRestorePolicy.ResolveDefaultPageLayout("TwoPageWithCover")
            .Should().Be(PageLayoutMode.TwoPageWithCover);
        DocumentViewRestorePolicy.ResolveDefaultPageLayout(null)
            .Should().Be(PageLayoutMode.Continuous);
    }
}
