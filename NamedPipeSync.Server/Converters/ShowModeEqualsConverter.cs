using System;
using System.Globalization;
using System.Windows.Data;
using NamedPipeSync.Common.Application;

namespace NamedPipeSync.Server.Converters;

/// <summary>
/// Converts a ShowMode value and a converter parameter to a bool indicating equality.
/// One-way converter intended for RadioButton.IsChecked bindings.
/// </summary>
public sealed class ShowModeEqualsConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is ShowMode mode && parameter is string param && Enum.TryParse<ShowMode>(param, out var p))
        {
            return mode == p;
        }
        return false;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        // If the RadioButton becomes checked (true), return the ShowMode represented by the parameter.
        // If it becomes unchecked, do not change the source (return Binding.DoNothing).
        if (value is bool isChecked && isChecked && parameter is string param &&
            Enum.TryParse<ShowMode>(param, out var mode))
        {
            return mode;
        }

        return Binding.DoNothing;
    }
}
