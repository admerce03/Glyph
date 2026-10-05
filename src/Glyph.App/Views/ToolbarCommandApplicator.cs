using Glyph.Infrastructure.Settings;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Glyph.App.Views;

/// <summary>
/// Shared F54 toolbar hide/reorder for PDF and image document toolbars.
/// Tags use <see cref="ToolbarCommands"/> ids on <see cref="FrameworkElement.Tag"/>.
/// Pass <paramref name="defaults"/> to re-apply from a full catalog snapshot
/// (required after Preferences change so previously hidden controls can return).
/// </summary>
internal static class ToolbarCommandApplicator
{
    public static void Tag(FrameworkElement element, string commandId) =>
        element.Tag = commandId;

    public static void Apply(
        StackPanel? toolbar,
        AppSettings? settings,
        IReadOnlyList<UIElement>? defaults = null)
    {
        if (toolbar is null)
        {
            return;
        }

        if (settings is not null)
        {
            toolbar.Spacing = settings.CompactToolbar ? 2 : 6;
            toolbar.Padding = settings.CompactToolbar
                ? new Thickness(4, 2, 4, 2)
                : new Thickness(8);
        }

        var children = defaults is { Count: > 0 }
            ? defaults.ToList()
            : toolbar.Children.Cast<UIElement>().ToList();
        var reordered = ToolbarOrderPolicy.ApplyVisibilityAndOrder(
            children,
            element => element is FrameworkElement { Tag: string id } ? id : null,
            settings?.ToolbarHiddenCommands,
            settings?.ToolbarCommandOrder);

        toolbar.Children.Clear();
        foreach (var child in reordered)
        {
            toolbar.Children.Add(child);
        }
    }
}
