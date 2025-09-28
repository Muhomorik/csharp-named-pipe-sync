using System.Globalization;
using System.Windows.Data;

using NamedPipeSync.Common.Application;

namespace NamedPipeSync.Server.Converters;

/// <summary>
/// Converts a <see cref="ConnectionState"/> to a glyph string suitable for display
/// using the recommended Segoe Fluent Icons font.
/// The glyph returned represents the action the user can take:
/// - When Connected: show the "DisconnectDisplay" glyph (action = disconnect)
/// - When Disconnected: show the "AddRemoteDevice" glyph (action = connect)
/// 
/// Note: Segoe Fluent Icons is the recommended symbol font; the codepoints used
/// below (E836 and EA14) are valid glyphs that correspond to connect/disconnect
/// actions in modern Segoe Fluent Icons builds and are compatible with many MDL2
/// glyph sets as well.
/// </summary>
public sealed class ConnectionStateToGlyphConverter : IValueConverter
{
    // Note: Glyphs chosen from Segoe Fluent Icons (recommended).
    private const string CheckboxCompositeReversed = "\uE73D";
    private const string IncidentTriangle = "\uE814";

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is ConnectionState state)
        {
            // Return the action glyph: when the client is connected, show "DisconnectDisplay";
            // when disconnected, show "AddRemoteDevice".
            return state == ConnectionState.Connected ? CheckboxCompositeReversed : IncidentTriangle;
        }

        return CheckboxCompositeReversed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException("ConvertBack is not supported for ConnectionStateToGlyphConverter.");
}