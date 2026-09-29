using System.Globalization;
using System.Windows.Data;

namespace Cedx.App.Common;

public sealed class TileWidthConverter : IValueConverter, IMultiValueConverter
{
    private const double MinimumTileWidth = 280d;
    private const double TileGap = 12d;
    private const double ViewPaddingAndScrollbar = 42d;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return CalculateWidth(value as double?, null);
    }

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        var actualWidth = values.Length > 0 ? values[0] as double? : null;
        var itemCount = values.Length > 1 ? values[1] as int? : null;
        return CalculateWidth(actualWidth, itemCount, values.Length > 2 ? values[2] as double? : null);
    }

    private static double CalculateWidth(double? actualWidth, int? itemCount, double? requestedMinimum = null)
    {
        if (actualWidth is not double width || double.IsNaN(width) || width <= 0)
        {
            return MinimumTileWidth;
        }

        var available = Math.Max(80, width - ViewPaddingAndScrollbar);
        var columns = Math.Max(1, (int)Math.Floor(available / ((requestedMinimum ?? MinimumTileWidth) + TileGap)));
        if (itemCount is > 0)
        {
            columns = Math.Min(columns, itemCount.Value);
        }

        return Math.Max(60, Math.Floor((available / columns) - TileGap - 30));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

