using Glyph.Core.Documents;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace Glyph.App;

/// <summary>
/// Maps <see cref="KeyboardGestureSyntax"/> strings onto WinUI accelerators / key events.
/// </summary>
internal static class WinUiKeyboardGestures
{
    public static bool TryCreateAccelerator(string? gesture, out KeyboardAccelerator accelerator)
    {
        accelerator = null!;
        if (!KeyboardGestureSyntax.TryParse(gesture, out var parsed)
            || !TryMapKey(parsed.Key, out var key))
        {
            return false;
        }

        var modifiers = VirtualKeyModifiers.None;
        if (parsed.Ctrl)
        {
            modifiers |= VirtualKeyModifiers.Control;
        }

        if (parsed.Shift)
        {
            modifiers |= VirtualKeyModifiers.Shift;
        }

        if (parsed.Alt)
        {
            modifiers |= VirtualKeyModifiers.Menu;
        }

        accelerator = new KeyboardAccelerator { Key = key, Modifiers = modifiers };
        return true;
    }

    public static bool Matches(
        string? gesture,
        VirtualKey key,
        bool ctrl,
        bool shift,
        bool alt)
    {
        if (!TryMapKeyToken(key, out var token))
        {
            return false;
        }

        return KeyboardGestureSyntax.Matches(gesture, ctrl, shift, alt, token);
    }

    public static bool MatchesCommand(
        string command,
        IReadOnlyDictionary<string, string>? overrides,
        VirtualKey key,
        bool ctrl,
        bool shift,
        bool alt)
    {
        var gesture = ShortcutCustomizationPolicy.Resolve(command, overrides);
        return Matches(gesture, key, ctrl, shift, alt);
    }

    private static bool TryMapKey(string keyToken, out VirtualKey key)
    {
        key = VirtualKey.None;
        switch (keyToken)
        {
            case "+":
                key = (VirtualKey)187; // OemPlus
                return true;
            case "-":
                key = (VirtualKey)189; // OemMinus
                return true;
            case "OemComma":
                key = (VirtualKey)188;
                return true;
            case "Tab":
                key = VirtualKey.Tab;
                return true;
            case "Delete":
                key = VirtualKey.Delete;
                return true;
            case "Back":
                key = VirtualKey.Back;
                return true;
            case "PageUp":
                key = VirtualKey.PageUp;
                return true;
            case "PageDown":
                key = VirtualKey.PageDown;
                return true;
        }

        if (keyToken.Length >= 2 && keyToken[0] == 'F' && int.TryParse(keyToken[1..], out var fn)
            && fn is >= 1 and <= 24)
        {
            key = VirtualKey.F1 + (fn - 1);
            return true;
        }

        if (keyToken.Length == 1)
        {
            var ch = keyToken[0];
            if (ch is >= 'A' and <= 'Z')
            {
                key = VirtualKey.A + (ch - 'A');
                return true;
            }

            if (ch is >= '0' and <= '9')
            {
                key = VirtualKey.Number0 + (ch - '0');
                return true;
            }
        }

        return false;
    }

    private static bool TryMapKeyToken(VirtualKey key, out string token)
    {
        token = string.Empty;
        if (key is VirtualKey.Add or (VirtualKey)187)
        {
            token = "+";
            return true;
        }

        if (key is VirtualKey.Subtract or (VirtualKey)189)
        {
            token = "-";
            return true;
        }

        if (key == (VirtualKey)188)
        {
            token = "OemComma";
            return true;
        }

        if (key is >= VirtualKey.F1 and <= VirtualKey.F24)
        {
            token = "F" + (1 + (key - VirtualKey.F1));
            return true;
        }

        if (key is >= VirtualKey.A and <= VirtualKey.Z)
        {
            token = ((char)('A' + (key - VirtualKey.A))).ToString();
            return true;
        }

        if (key is >= VirtualKey.Number0 and <= VirtualKey.Number9)
        {
            token = ((char)('0' + (key - VirtualKey.Number0))).ToString();
            return true;
        }

        token = key switch
        {
            VirtualKey.Tab => "Tab",
            VirtualKey.Delete => "Delete",
            VirtualKey.Back => "Back",
            VirtualKey.PageUp => "PageUp",
            VirtualKey.PageDown => "PageDown",
            _ => string.Empty,
        };
        return token.Length > 0;
    }
}
