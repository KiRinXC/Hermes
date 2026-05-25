using System.Windows.Input;
using Hermes.Windows.Infrastructure;

namespace Hermes.Windows.Input;

public sealed record HotkeyGesture(bool Control, bool Alt, bool Shift, bool Win, Key Key)
{
    public static HotkeyGesture Default { get; } = new(Control: true, Alt: true, Shift: false, Win: false, Key.E);

    public uint Modifiers
    {
        get
        {
            uint modifiers = NativeMethods.ModNoRepeat;
            if (Control)
            {
                modifiers |= NativeMethods.ModControl;
            }

            if (Alt)
            {
                modifiers |= NativeMethods.ModAlt;
            }

            if (Shift)
            {
                modifiers |= NativeMethods.ModShift;
            }

            if (Win)
            {
                modifiers |= NativeMethods.ModWin;
            }

            return modifiers;
        }
    }

    public uint VirtualKey => (uint)KeyInterop.VirtualKeyFromKey(Key);

    public static HotkeyGesture ParseOrDefault(string? value)
    {
        return TryParse(value, out var gesture) ? gesture : Default;
    }

    public static bool TryParse(string? value, out HotkeyGesture gesture)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            gesture = Default;
            return false;
        }

        var control = false;
        var alt = false;
        var shift = false;
        var win = false;
        Key? key = null;

        foreach (var part in value.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            switch (part.ToLowerInvariant())
            {
                case "ctrl":
                case "control":
                    control = true;
                    break;
                case "alt":
                    alt = true;
                    break;
                case "shift":
                    shift = true;
                    break;
                case "win":
                case "windows":
                    win = true;
                    break;
                default:
                    if (Enum.TryParse<Key>(part, ignoreCase: true, out var parsed))
                    {
                        key = parsed;
                    }

                    break;
            }
        }

        if (key is null || (!control && !alt && !shift && !win))
        {
            gesture = Default;
            return false;
        }

        gesture = new HotkeyGesture(control, alt, shift, win, key.Value);
        return true;
    }

    public override string ToString()
    {
        var parts = new List<string>();
        if (Control)
        {
            parts.Add("Ctrl");
        }

        if (Alt)
        {
            parts.Add("Alt");
        }

        if (Shift)
        {
            parts.Add("Shift");
        }

        if (Win)
        {
            parts.Add("Win");
        }

        parts.Add(Key.ToString());
        return string.Join("+", parts);
    }
}
