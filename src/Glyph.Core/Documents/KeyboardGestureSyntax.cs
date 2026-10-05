namespace Glyph.Core.Documents;

/// <summary>
/// Parse/normalize keyboard gesture strings used by shortcut customization (F52).
/// Format: optional modifiers (<c>Ctrl</c>/<c>Shift</c>/<c>Alt</c>) then a key token,
/// joined by <c>+</c> (e.g. <c>Ctrl+Shift+S</c>, <c>F11</c>, <c>Ctrl++</c>).
/// </summary>
public static class KeyboardGestureSyntax
{
    public readonly record struct Parsed(bool Ctrl, bool Shift, bool Alt, string Key);

    public static string Normalize(string? gesture)
    {
        if (!TryParse(gesture, out var parsed))
        {
            return (gesture ?? string.Empty).Trim();
        }

        return Format(parsed);
    }

    public static string Format(Parsed parsed)
    {
        var parts = new List<string>(4);
        if (parsed.Ctrl)
        {
            parts.Add("Ctrl");
        }

        if (parsed.Shift)
        {
            parts.Add("Shift");
        }

        if (parsed.Alt)
        {
            parts.Add("Alt");
        }

        parts.Add(parsed.Key);
        return string.Join('+', parts);
    }

    public static bool TryParse(string? gesture, out Parsed parsed)
    {
        parsed = default;
        if (string.IsNullOrWhiteSpace(gesture))
        {
            return false;
        }

        var raw = gesture.Trim();
        // "Ctrl++" → modifiers + key "+"
        var tokens = new List<string>();
        var i = 0;
        while (i < raw.Length)
        {
            if (raw[i] == '+')
            {
                // Lone/trailing + is the Plus key when we already have a token boundary.
                if (tokens.Count > 0 && (i + 1 >= raw.Length || raw[i + 1] == '+'))
                {
                    tokens.Add("+");
                    i++;
                    if (i < raw.Length && raw[i] == '+')
                    {
                        i++; // consume separator after Plus key in Ctrl+++Shift (rare)
                    }

                    continue;
                }

                i++;
                continue;
            }

            var start = i;
            while (i < raw.Length && raw[i] != '+')
            {
                i++;
            }

            var token = raw[start..i].Trim();
            if (token.Length > 0)
            {
                tokens.Add(token);
            }

            if (i < raw.Length && raw[i] == '+')
            {
                i++;
            }
        }

        if (tokens.Count == 0)
        {
            return false;
        }

        var ctrl = false;
        var shift = false;
        var alt = false;
        string? key = null;
        foreach (var token in tokens)
        {
            if (token.Equals("Ctrl", StringComparison.OrdinalIgnoreCase)
                || token.Equals("Control", StringComparison.OrdinalIgnoreCase))
            {
                ctrl = true;
                continue;
            }

            if (token.Equals("Shift", StringComparison.OrdinalIgnoreCase))
            {
                shift = true;
                continue;
            }

            if (token.Equals("Alt", StringComparison.OrdinalIgnoreCase)
                || token.Equals("Menu", StringComparison.OrdinalIgnoreCase))
            {
                alt = true;
                continue;
            }

            if (key is not null)
            {
                return false;
            }

            key = CanonicalKey(token);
            if (key is null)
            {
                return false;
            }
        }

        if (key is null)
        {
            return false;
        }

        parsed = new Parsed(ctrl, shift, alt, key);
        return true;
    }

    public static bool Matches(string? gesture, bool ctrl, bool shift, bool alt, string keyToken)
    {
        if (!TryParse(gesture, out var parsed))
        {
            return false;
        }

        var key = CanonicalKey(keyToken);
        return key is not null
            && parsed.Ctrl == ctrl
            && parsed.Shift == shift
            && parsed.Alt == alt
            && string.Equals(parsed.Key, key, StringComparison.OrdinalIgnoreCase);
    }

    private static string? CanonicalKey(string token)
    {
        var t = token.Trim();
        if (t.Length == 0)
        {
            return null;
        }

        if (t is "+" or "=")
        {
            return "+";
        }

        if (t is "-" or "_")
        {
            return "-";
        }

        if (t.Equals("OemComma", StringComparison.OrdinalIgnoreCase)
            || t.Equals("Comma", StringComparison.OrdinalIgnoreCase)
            || t == ",")
        {
            return "OemComma";
        }

        if (t.Equals("PageUp", StringComparison.OrdinalIgnoreCase)
            || t.Equals("Prior", StringComparison.OrdinalIgnoreCase))
        {
            return "PageUp";
        }

        if (t.Equals("PageDown", StringComparison.OrdinalIgnoreCase)
            || t.Equals("Next", StringComparison.OrdinalIgnoreCase))
        {
            return "PageDown";
        }

        if (t.Equals("Delete", StringComparison.OrdinalIgnoreCase)
            || t.Equals("Del", StringComparison.OrdinalIgnoreCase))
        {
            return "Delete";
        }

        if (t.Equals("Back", StringComparison.OrdinalIgnoreCase)
            || t.Equals("Backspace", StringComparison.OrdinalIgnoreCase))
        {
            return "Back";
        }

        if (t.Equals("Tab", StringComparison.OrdinalIgnoreCase))
        {
            return "Tab";
        }

        if (t.Length >= 2 && (t[0] is 'F' or 'f') && int.TryParse(t[1..], out var fn) && fn is >= 1 and <= 24)
        {
            return "F" + fn;
        }

        if (t.Length == 1 && char.IsLetterOrDigit(t[0]))
        {
            return char.ToUpperInvariant(t[0]).ToString();
        }

        return null;
    }
}
