using System.Globalization;
using System.Windows.Data;

using NamedPipeSync.Common.Application;

namespace NamedPipeSync.Server.Converters;

/// <summary>
/// Converter that maps connection state values to a glyph used in the Action column.
/// This is intentionally separate from ConnectionStateToGlyphConverter so the Action
/// column can use different icons (or styles) from the Connection column.
/// </summary>
public class ActionStateToGlyphConverter : IValueConverter
{
    // Segoe Fluent Icons.
    private const string Cancel = "\uE711";
    private const string OpenInNewWindow = "\uE8A7";
    private const string UnknownGlyph = "\uE11B"; // example glyph for unknown/neutral

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is ConnectionState state)
        {
            return state switch
            {
                ConnectionState.Connected => Cancel,
                ConnectionState.Disconnected => OpenInNewWindow,
                _ => UnknownGlyph
            };
        }

        return UnknownGlyph;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        // One-way converter; ConvertBack is not implemented.
        throw new NotSupportedException();
    }
}