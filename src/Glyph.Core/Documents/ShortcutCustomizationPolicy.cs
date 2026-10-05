namespace Glyph.Core.Documents;

/// <summary>
/// User-overridable keyboard shortcuts (FEATURES.md §52 “configurable eventually”).
/// Defaults come from <see cref="ShellKeyboardShortcuts"/> and <see cref="DocumentKeyboardShortcuts"/>.
/// </summary>
public static class ShortcutCustomizationPolicy
{
    public static IReadOnlyList<(string Command, string DefaultGesture)> DefaultCatalog { get; } =
        BuildDefaultCatalog();

    public static string Resolve(
        string command,
        IReadOnlyDictionary<string, string>? overrides)
    {
        var defaults = DefaultCatalog;
        var match = defaults.FirstOrDefault(c =>
            string.Equals(c.Command, command, StringComparison.OrdinalIgnoreCase));
        if (match.Command is null)
        {
            return string.Empty;
        }

        if (overrides is not null
            && overrides.TryGetValue(match.Command, out var over)
            && !string.IsNullOrWhiteSpace(over))
        {
            return KeyboardGestureSyntax.Normalize(over);
        }

        return match.DefaultGesture;
    }

    public static IReadOnlyDictionary<string, string> EffectiveMap(
        IReadOnlyDictionary<string, string>? overrides)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (command, _) in DefaultCatalog)
        {
            map[command] = Resolve(command, overrides);
        }

        return map;
    }

    /// <summary>
    /// Keep only known commands whose gesture differs from the default and parses cleanly.
    /// </summary>
    public static Dictionary<string, string> NormalizeOverrides(
        IEnumerable<KeyValuePair<string, string>>? overrides)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (overrides is null)
        {
            return result;
        }

        var defaults = DefaultCatalog.ToDictionary(
            c => c.Command,
            c => c.DefaultGesture,
            StringComparer.OrdinalIgnoreCase);

        foreach (var (command, gesture) in overrides)
        {
            if (string.IsNullOrWhiteSpace(command) || !defaults.ContainsKey(command.Trim()))
            {
                continue;
            }

            var name = defaults.Keys.First(k =>
                string.Equals(k, command.Trim(), StringComparison.OrdinalIgnoreCase));
            var normalized = KeyboardGestureSyntax.Normalize(gesture);
            if (!KeyboardGestureSyntax.TryParse(normalized, out _))
            {
                continue;
            }

            if (string.Equals(normalized, defaults[name], StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            result[name] = normalized;
        }

        return result;
    }

    public static string? ValidationError(IReadOnlyDictionary<string, string>? overrides)
    {
        var effective = EffectiveMap(overrides);
        var seen = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (command, gesture) in effective)
        {
            if (!KeyboardGestureSyntax.TryParse(gesture, out _))
            {
                return $"Invalid gesture for '{command}': {gesture}";
            }

            if (seen.TryGetValue(gesture, out var other))
            {
                return $"Gesture '{gesture}' is assigned to both '{other}' and '{command}'";
            }

            seen[gesture] = command;
        }

        return null;
    }

    private static IReadOnlyList<(string Command, string DefaultGesture)> BuildDefaultCatalog()
    {
        var list = new List<(string Command, string DefaultGesture)>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in ShellKeyboardShortcuts.Catalog.Concat(DocumentKeyboardShortcuts.Catalog))
        {
            if (!seen.Add(entry.Command))
            {
                continue;
            }

            list.Add((entry.Command, KeyboardGestureSyntax.Normalize(entry.Gesture)));
        }

        return list;
    }
}
