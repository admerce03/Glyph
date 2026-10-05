using FluentAssertions;
using Glyph.Core.Documents;
using Glyph.Core.Printing;

namespace Glyph.Core.Tests;

public class PageContextMenuTests
{
    [Fact]
    public void Text_and_annotation_commands()
    {
        PageContextMenu.TextSelectionCommands.Should().Contain(PageContextMenu.Highlight);
        PageContextMenu.TextSelectionCommands.Should().Contain(PageContextMenu.SearchWeb);
        PageContextMenu.AnnotationCommands.Should().Contain(PageContextMenu.Style);
        PageContextMenu.AnnotationCommands.Should().Contain(PageContextMenu.Duplicate);
    }
}

public class TouchpadGesturePolicyTests
{
    [Fact]
    public void Pinch_and_scroll_mappings()
    {
        TouchpadGesturePolicy.TwoFingerScrollUsesScrollViewer.Should().BeTrue();
        TouchpadGesturePolicy.PreferPinchZoom(controlModifierDown: true, manipulationScale: false).Should().BeTrue();
        TouchpadGesturePolicy.PreferPinchZoom(false, true).Should().BeTrue();
        TouchpadGesturePolicy.PreferPinchZoom(false, false).Should().BeFalse();
        TouchpadGesturePolicy.CustomTouchscreenGestures.Should().BeFalse();
    }
}

public class PrintSystemCapabilitiesTests
{
    [Fact]
    public void System_ui_catalog()
    {
        PrintSystemCapabilities.CatalogContains("Copies").Should().BeTrue();
        PrintSystemCapabilities.CatalogContains("Duplex").Should().BeTrue();
        PrintSystemCapabilities.UsesImageableRectMargins.Should().BeTrue();
        PrintSystemCapabilities.AnnotationsIncludedInPageRender.Should().BeTrue();
    }
}

public class AccessibilityPolicyTests
{
    [Fact]
    public void Chrome_a11y_posture()
    {
        AccessibilityPolicy.KeyboardAccessibleChrome.Should().BeTrue();
        AccessibilityPolicy.ScreenReaderLabeledSurfaces.Should().Contain("Toolbar");
        AccessibilityPolicy.ScreenReaderLabeledSurfaces.Should().Contain("Help");
        AccessibilityPolicy.ZoomScalesPageBitmapsOnly.Should().BeTrue();
    }
}
