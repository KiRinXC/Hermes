using MediaColor = System.Windows.Media.Color;
using MediaColors = System.Windows.Media.Colors;

namespace Hermes.Windows.Shell;

public sealed record SettingsWindowThemePalette(
    MediaColor Shell,
    MediaColor TextPrimary,
    MediaColor TextSecondary,
    MediaColor TextMuted,
    MediaColor Card,
    MediaColor Control,
    MediaColor ControlStrong,
    MediaColor Border,
    MediaColor Divider,
    MediaColor FaintDivider,
    MediaColor Keycap,
    MediaColor KeycapBorder,
    MediaColor ToggleTrack,
    MediaColor SliderTrack,
    MediaColor SliderThumb,
    MediaColor Accent,
    MediaColor AccentHover,
    MediaColor Success,
    MediaColor Warning,
    MediaColor BlueSoft,
    MediaColor OuterStrokeTop,
    MediaColor OuterStrokeBottom);

public static class SettingsWindowThemePalettes
{
    public static SettingsWindowThemePalette For(string? theme)
    {
        return string.Equals(theme, "Light", StringComparison.OrdinalIgnoreCase)
            ? Light
            : Dark;
    }

    public static SettingsWindowThemePalette Dark { get; } = new(
        Shell: MediaColor.FromArgb(0xF2, 0x0A, 0x0A, 0x0C),
        TextPrimary: MediaColors.White,
        TextSecondary: MediaColor.FromRgb(0xD1, 0xD1, 0xD6),
        TextMuted: MediaColor.FromRgb(0x8E, 0x8E, 0x93),
        Card: MediaColor.FromArgb(0x08, 0xFF, 0xFF, 0xFF),
        Control: MediaColor.FromArgb(0x0A, 0xFF, 0xFF, 0xFF),
        ControlStrong: MediaColor.FromArgb(0x33, 0x00, 0x00, 0x00),
        Border: MediaColor.FromRgb(0x2C, 0x2C, 0x2E),
        Divider: MediaColor.FromArgb(0x14, 0x2C, 0x2C, 0x2E),
        FaintDivider: MediaColor.FromArgb(0x05, 0xFF, 0xFF, 0xFF),
        Keycap: MediaColor.FromRgb(0x1B, 0x1B, 0x1F),
        KeycapBorder: MediaColor.FromRgb(0x2C, 0x2C, 0x2E),
        ToggleTrack: MediaColor.FromRgb(0x2C, 0x2C, 0x2E),
        SliderTrack: MediaColor.FromRgb(0x2C, 0x2C, 0x2E),
        SliderThumb: MediaColors.White,
        Accent: MediaColor.FromRgb(0x00, 0x7A, 0xFF),
        AccentHover: MediaColor.FromRgb(0x33, 0x95, 0xFF),
        Success: MediaColor.FromRgb(0x30, 0xD1, 0x58),
        Warning: MediaColor.FromRgb(0xFF, 0xB7, 0x4D),
        BlueSoft: MediaColor.FromRgb(0x64, 0xB5, 0xF6),
        OuterStrokeTop: MediaColor.FromRgb(0x2C, 0x2C, 0x30),
        OuterStrokeBottom: MediaColor.FromRgb(0x1A, 0x1A, 0x1E));

    public static SettingsWindowThemePalette Light { get; } = new(
        Shell: MediaColor.FromArgb(0xF4, 0xF5, 0xF5, 0xF7),
        TextPrimary: MediaColor.FromRgb(0x1D, 0x1D, 0x1F),
        TextSecondary: MediaColor.FromRgb(0x3A, 0x3A, 0x3C),
        TextMuted: MediaColor.FromRgb(0x6E, 0x6E, 0x73),
        Card: MediaColor.FromArgb(0xCC, 0xFF, 0xFF, 0xFF),
        Control: MediaColor.FromArgb(0xD9, 0xFF, 0xFF, 0xFF),
        ControlStrong: MediaColor.FromArgb(0x0A, 0x00, 0x00, 0x00),
        Border: MediaColor.FromRgb(0xD1, 0xD1, 0xD6),
        Divider: MediaColor.FromArgb(0x24, 0x00, 0x00, 0x00),
        FaintDivider: MediaColor.FromArgb(0x12, 0x00, 0x00, 0x00),
        Keycap: MediaColor.FromRgb(0xF2, 0xF2, 0xF7),
        KeycapBorder: MediaColor.FromRgb(0xD1, 0xD1, 0xD6),
        ToggleTrack: MediaColor.FromRgb(0xE5, 0xE5, 0xEA),
        SliderTrack: MediaColor.FromRgb(0xC7, 0xC7, 0xCC),
        SliderThumb: MediaColors.White,
        Accent: MediaColor.FromRgb(0x00, 0x7A, 0xFF),
        AccentHover: MediaColor.FromRgb(0x00, 0x68, 0xD9),
        Success: MediaColor.FromRgb(0x34, 0xC7, 0x59),
        Warning: MediaColor.FromRgb(0xC2, 0x6A, 0x00),
        BlueSoft: MediaColor.FromRgb(0x00, 0x64, 0xC8),
        OuterStrokeTop: MediaColor.FromRgb(0xFF, 0xFF, 0xFF),
        OuterStrokeBottom: MediaColor.FromRgb(0xD1, 0xD1, 0xD6));
}
