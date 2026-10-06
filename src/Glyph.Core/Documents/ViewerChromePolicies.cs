namespace Glyph.Core.Documents;

/// <summary>
/// Required AutomationProperties.Name values on PDF viewer chrome (F56-08).
/// </summary>
public static class ViewerToolbarAutomationNames
{
    public static IReadOnlyList<string> RequiredNames { get; } =
    [
        "Magnifier loupe",
        "Zoom to area",
        "Presentation mode",
        "Sidebar mode",
        "Table of contents",
        "Match case",
        "Match any word",
        "Sort find results",
    ];
}

/// <summary>
/// System title bar expectation (F02-01).
/// </summary>
public static class SystemTitleBarPolicy
{
    /// <summary>Glyph uses the OS AppWindow title bar (not a custom caption).</summary>
    public const bool UsesSystemTitleBar = true;
}

/// <summary>
/// Primary mouse pointer interaction (F02-21).
/// </summary>
public static class PointerInputPolicy
{
    public static bool IsPrimaryButton(bool isLeftButtonPressed) => isLeftButtonPressed;
}
