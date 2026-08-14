using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace VPSMonitor.App.Converters;

public sealed class PageSelectionBrushConverter : IValueConverter
{
    private static readonly System.Windows.Media.Brush SelectedBrush =
        new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(25, 48, 71));
    private static readonly System.Windows.Media.Brush TransparentBrush = System.Windows.Media.Brushes.Transparent;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        string.Equals(value as string, parameter as string, StringComparison.Ordinal)
            ? SelectedBrush
            : TransparentBrush;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        System.Windows.Data.Binding.DoNothing;
}
