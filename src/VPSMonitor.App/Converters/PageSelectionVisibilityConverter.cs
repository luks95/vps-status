using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace VPSMonitor.App.Converters;

/// <summary>Muestra un elemento solo cuando la página actual coincide con el parámetro.</summary>
public sealed class PageSelectionVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        string.Equals(value as string, parameter as string, StringComparison.Ordinal)
            ? Visibility.Visible
            : Visibility.Hidden;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        System.Windows.Data.Binding.DoNothing;
}

/// <summary>Resalta el texto de la página seleccionada con mayor peso tipográfico.</summary>
public sealed class PageSelectionWeightConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        string.Equals(value as string, parameter as string, StringComparison.Ordinal)
            ? FontWeights.SemiBold
            : FontWeights.Normal;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        System.Windows.Data.Binding.DoNothing;
}
