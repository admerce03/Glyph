using Glyph.Infrastructure.Settings;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Glyph.App.Views;

/// <summary>
/// Shared F54 toolbar hide/reorder for PDF and image document toolbars.
/// Tags use <see cref="ToolbarCommands"/> ids on <see cref="FrameworkElement.Tag"/>.
/// </summary>
internal static class ToolbarCommandApplicator
{
    public static void Tag(FrameworkElement element, string commandId) =>
        element.Tag = commandId;

    public static void Apply(StackPanel? toolbar, AppSettings? settings)
    {
        if (toolbar is null)
        {
            return;
        }

        var children = toolbar.Children.Cast<UIElement>().ToList();
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
