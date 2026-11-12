using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;

namespace VPSMonitor.App.Converters;

/// <summary>Colorea una barra de uso: acento normal, ámbar desde 75 % y rojo desde 90 %.</summary>
public sealed class UsageBrushConverter : IValueConverter
{
    private static readonly Brush Normal = Frozen(Color.FromRgb(0x27, 0xC2, 0xC1));
    private static readonly Brush Warning = Frozen(Color.FromRgb(0xFF, 0xB0, 0x20));
    private static readonly Brush Critical = Frozen(Color.FromRgb(0xFF, 0x5C, 0x6C));

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var percent = value is double number ? number : 0;
        return percent >= 90 ? Critical : percent >= 75 ? Warning : Normal;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        System.Windows.Data.Binding.DoNothing;

    private static Brush Frozen(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
